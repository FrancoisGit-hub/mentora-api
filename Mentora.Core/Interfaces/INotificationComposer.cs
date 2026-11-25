using Mentora.Core.Enums;

namespace Mentora.Core.Interfaces;

/// <summary>
/// Lot 14.9b. Builds NOTIFICATIONS rows for the business events. Every method only ADDS entities to the
/// MentoraDbContext of the calling service (same scope): it never calls SaveChanges, so the rows are saved
/// (or rolled back) with the business action. At most one database query per call. A recipient whose
/// preference is off gets no row. The actor never receives a notification for his own action.
/// </summary>
public interface INotificationComposer
{
    /// <summary>CHAT_MESSAGE to the other party. textContent null means an audio message.</summary>
    Task AddChatMessageAsync(Guid memberId, Guid coachId, MessageSenderType senderType, string? textContent, CancellationToken ct);

    /// <summary>SESSION_BOOKED to the coach (member reserved). Preference NotifNewBooking.</summary>
    Task AddSessionBookedForCoachAsync(Guid sessionId, Guid memberId, Guid coachId, DateTime scheduledAtUtc, CancellationToken ct);

    /// <summary>SESSION_BOOKED to the member (coach registered him in a group session). No preference.</summary>
    Task AddSessionBookedForMemberAsync(Guid sessionId, Guid memberId, Guid coachId, DateTime scheduledAtUtc, CancellationToken ct);

    /// <summary>SESSION_CANCELLED to the coach (member cancelled). Preference NotifBookingCancelled.</summary>
    Task AddSessionCancelledForCoachAsync(Guid sessionId, Guid memberId, Guid coachId, DateTime scheduledAtUtc, CancellationToken ct);

    /// <summary>SESSION_CANCELLED to the member (coach cancelled or removed him). No preference.</summary>
    Task AddSessionCancelledForMemberAsync(Guid sessionId, Guid memberId, Guid coachId, DateTime scheduledAtUtc, CancellationToken ct);

    /// <summary>MEMBER_JOINED to every coach linked to the member. No preference.</summary>
    Task AddMemberJoinedAsync(Guid memberId, string firstName, string lastName, CancellationToken ct);
}
