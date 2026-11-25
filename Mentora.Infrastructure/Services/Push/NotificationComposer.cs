using System.Text.Json;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Mentora.Core.Notifications;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services.Push;

/// <summary>
/// Lot 14.9b. Scoped, shares the MentoraDbContext of the calling service and only ADDS Notification rows:
/// no SaveChanges here, the caller's own save (inside its transaction) writes them. One query per call
/// (names, recipient user ids and preferences in a single projection). A missing MEMBER_PARAMETERS or
/// COACH_PARAMETERS row counts as the entity defaults (true).
/// </summary>
public sealed class NotificationComposer(MentoraDbContext db) : INotificationComposer
{
    private sealed record Pair(
        Guid MemberUserId, string MemberFirstName, string MemberLastName, bool MemberNotifMessages,
        Guid CoachUserId, string CoachFirstName, string CoachLastName,
        bool CoachNotifMessages, bool CoachNotifNewBooking, bool CoachNotifBookingCancelled);

    public async Task AddChatMessageAsync(
        Guid memberId, Guid coachId, MessageSenderType senderType, string? textContent, CancellationToken ct)
    {
        var p = await LoadPairAsync(memberId, coachId, ct);
        if (p is null)
            return;

        var body = NotificationTexts.ChatBody(textContent);
        if (senderType == MessageSenderType.Member)
        {
            if (!p.CoachNotifMessages)
                return;
            Add(p.CoachUserId, NotificationTypes.ChatMessage,
                NotificationTexts.FullName(p.MemberFirstName, p.MemberLastName), body,
                Data(NotificationTypes.ChatMessage, ("memberId", memberId)));
        }
        else
        {
            if (!p.MemberNotifMessages)
                return;
            Add(p.MemberUserId, NotificationTypes.ChatMessage,
                NotificationTexts.FullName(p.CoachFirstName, p.CoachLastName), body,
                Data(NotificationTypes.ChatMessage, ("coachId", coachId)));
        }
    }

    public async Task AddSessionBookedForCoachAsync(
        Guid sessionId, Guid memberId, Guid coachId, DateTime scheduledAtUtc, CancellationToken ct)
    {
        var p = await LoadPairAsync(memberId, coachId, ct);
        if (p is null || !p.CoachNotifNewBooking)
            return;

        var (title, body) = NotificationTexts.SessionBookedForCoach(
            NotificationTexts.FullName(p.MemberFirstName, p.MemberLastName), scheduledAtUtc);
        Add(p.CoachUserId, NotificationTypes.SessionBooked, title, body,
            Data(NotificationTypes.SessionBooked, ("sessionId", sessionId), ("memberId", memberId)));
    }

    public async Task AddSessionBookedForMemberAsync(
        Guid sessionId, Guid memberId, Guid coachId, DateTime scheduledAtUtc, CancellationToken ct)
    {
        var p = await LoadPairAsync(memberId, coachId, ct);
        if (p is null)
            return;

        var (title, body) = NotificationTexts.SessionBookedForMember(p.CoachFirstName, scheduledAtUtc);
        Add(p.MemberUserId, NotificationTypes.SessionBooked, title, body,
            Data(NotificationTypes.SessionBooked, ("sessionId", sessionId)));
    }

    public async Task AddSessionCancelledForCoachAsync(
        Guid sessionId, Guid memberId, Guid coachId, DateTime scheduledAtUtc, CancellationToken ct)
    {
        var p = await LoadPairAsync(memberId, coachId, ct);
        if (p is null || !p.CoachNotifBookingCancelled)
            return;

        var (title, body) = NotificationTexts.SessionCancelledForCoach(
            NotificationTexts.FullName(p.MemberFirstName, p.MemberLastName), scheduledAtUtc);
        Add(p.CoachUserId, NotificationTypes.SessionCancelled, title, body,
            Data(NotificationTypes.SessionCancelled, ("sessionId", sessionId), ("memberId", memberId)));
    }

    public async Task AddSessionCancelledForMemberAsync(
        Guid sessionId, Guid memberId, Guid coachId, DateTime scheduledAtUtc, CancellationToken ct)
    {
        var p = await LoadPairAsync(memberId, coachId, ct);
        if (p is null)
            return;

        var (title, body) = NotificationTexts.SessionCancelledForMember(p.CoachFirstName, scheduledAtUtc);
        Add(p.MemberUserId, NotificationTypes.SessionCancelled, title, body,
            Data(NotificationTypes.SessionCancelled, ("sessionId", sessionId)));
    }

    public async Task AddMemberJoinedAsync(Guid memberId, string firstName, string lastName, CancellationToken ct)
    {
        // One query: the user id of every coach linked to this member.
        var coachUserIds = await db.MemberCoaches
            .Where(mc => mc.MemberId == memberId)
            .Select(mc => mc.Coach.UserId)
            .Distinct()
            .ToListAsync(ct);

        var (title, body) = NotificationTexts.MemberJoined(NotificationTexts.FullName(firstName, lastName));
        foreach (var coachUserId in coachUserIds)
            Add(coachUserId, NotificationTypes.MemberJoined, title, body,
                Data(NotificationTypes.MemberJoined, ("memberId", memberId)));
    }

    private async Task<Pair?> LoadPairAsync(Guid memberId, Guid coachId, CancellationToken ct) =>
        await (from m in db.Members
               from c in db.Coaches
               where m.MemberId == memberId && c.CoachId == coachId
               select new Pair(
                   m.UserId, m.MemberFirstName, m.MemberLastName,
                   db.MemberParameters.Where(x => x.MemberId == m.MemberId)
                       .Select(x => (bool?)x.MemberParameterNotifMessages).FirstOrDefault() ?? true,
                   c.UserId, c.CoachFirstName, c.CoachLastName,
                   db.CoachParameters.Where(x => x.CoachId == c.CoachId)
                       .Select(x => (bool?)x.CoachParameterNotifMessages).FirstOrDefault() ?? true,
                   db.CoachParameters.Where(x => x.CoachId == c.CoachId)
                       .Select(x => (bool?)x.CoachParameterNotifNewBooking).FirstOrDefault() ?? true,
                   db.CoachParameters.Where(x => x.CoachId == c.CoachId)
                       .Select(x => (bool?)x.CoachParameterNotifBookingCancelled).FirstOrDefault() ?? true))
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    private void Add(Guid userId, string type, string title, string body, string dataJson)
    {
        var now = DateTime.UtcNow;
        db.Notifications.Add(new Notification
        {
            NotificationId              = Guid.NewGuid(),
            NotificationUserId          = userId,
            NotificationType            = type,
            NotificationTitle           = NotificationTexts.Fit(title, NotificationTexts.TitleMaxLength),
            NotificationBody            = NotificationTexts.Fit(body, NotificationTexts.BodyMaxLength),
            NotificationData            = dataJson,
            NotificationStatus          = NotificationStatuses.Pending,
            NotificationAttemptCount    = 0,
            NotificationNextAttemptDate = now,
            NotificationCreatedDate     = now,
            NotificationUpdatedDate     = now,
        });
    }

    /// <summary>Object of strings: type first, then the given ids. No notificationId (added at send time).</summary>
    private static string Data(string type, params (string Key, Guid Value)[] ids)
    {
        var data = new Dictionary<string, string> { ["type"] = type };
        foreach (var (key, value) in ids)
            data[key] = value.ToString();
        return JsonSerializer.Serialize(data);
    }
}
