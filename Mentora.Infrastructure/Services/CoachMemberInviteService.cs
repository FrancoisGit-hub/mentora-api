using FluentValidation;
using Mentora.Core;
using Mentora.Core.DTOs.Coach;
using Mentora.Core.Entities;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Mentora.Infrastructure.Persistence;
using Mentora.Infrastructure.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services;

/// <summary>
/// Lot 14.1: a coach adds a member by email. The whole write is one transaction serialized by an
/// advisory lock (same pattern as the exercise code allocation), so two simultaneous adds of the same
/// email cannot both create the account. The notification mail goes out after the commit, best effort.
/// </summary>
public class CoachMemberInviteService(
    MentoraDbContext db,
    IValidator<AddMemberRequest> validator,
    IEmailSender emailSender,
    IOptions<MemberInviteOptions> inviteOptions,
    ILogger<CoachMemberInviteService> logger) : ICoachMemberInviteService
{
    /// <summary>Fixed key of the advisory lock that serialises every member add.</summary>
    private const string AddMemberLockSql = "SELECT pg_advisory_xact_lock(7141401)";

    // The existing member rows store the role in upper case (scripts/seed-test-members.sql, local data).
    private const string MemberUserRole = "MEMBER";

    public async Task<AddMemberResponse> AddMemberAsync(
        Guid coachId, AddMemberRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var email = EmailNormalizer.Normalize(request.Email);
        var firstName = request.FirstName!.Trim();
        var lastName = request.LastName!.Trim();
        var phone = PhoneNormalizer.Normalize(request.Phone);

        AddMemberResponse response;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            // First statement of the transaction: everything below sees the committed result of any earlier add.
            await db.Database.ExecuteSqlRawAsync(AddMemberLockSql, ct);

            var user = await db.Users
                .Include(u => u.Coach)
                .Include(u => u.Member)
                .FirstOrDefaultAsync(u => u.UserEmail.ToLower() == email, ct);

            var now = DateTime.UtcNow;

            if (user is null)
            {
                var userId = Guid.NewGuid();
                var memberId = Guid.NewGuid();
                db.Users.Add(new User
                {
                    UserId = userId,
                    UserCreatedDate = now,
                    UserEmail = email,
                    UserIsEnabled = true,
                    UserRole = MemberUserRole
                });
                db.Members.Add(new Member
                {
                    MemberId = memberId,
                    UserId = userId,
                    MemberFirstName = firstName,
                    MemberLastName = lastName,
                    MemberCreatedDate = now,
                    MemberIsActive = true,
                    MemberHasActivated = false,
                    MemberActivationDate = null,
                    MemberPhone = phone,
                    MemberGender = request.Gender,
                    MemberHeightCm = request.HeightCm,
                    MemberBirthDate = request.BirthDate
                });
                db.MemberCoaches.Add(new MemberCoach
                {
                    MemberCoachId = Guid.NewGuid(),
                    MemberId = memberId,
                    CoachId = coachId,
                    IsPrimary = true,
                    StartedAt = now
                });
                await db.SaveChangesAsync(ct);
                response = new AddMemberResponse(
                    memberId, email, firstName, lastName, IsNewAccount: true, IsPrimary: true,
                    phone, request.Gender, request.HeightCm, request.BirthDate);
            }
            else
            {
                if (!user.UserIsEnabled)
                    throw Conflict("ACCOUNT_DISABLED", "This account is disabled.");
                if (user.Coach is not null)
                    throw Conflict("EMAIL_IS_COACH", "This email belongs to a coach account.");
                if (user.Member is null)
                    throw Conflict("NO_MEMBER_PROFILE", "This email has no member profile.");

                var member = user.Member;
                var links = await db.MemberCoaches
                    .Where(mc => mc.MemberId == member.MemberId)
                    .Select(mc => mc.CoachId)
                    .ToListAsync(ct);
                if (links.Contains(coachId))
                    throw Conflict("ALREADY_ATTACHED", "This member is already attached to this coach.");

                var isPrimary = links.Count == 0;
                db.MemberCoaches.Add(new MemberCoach
                {
                    MemberCoachId = Guid.NewGuid(),
                    MemberId = member.MemberId,
                    CoachId = coachId,
                    IsPrimary = isPrimary,
                    StartedAt = now
                });
                await db.SaveChangesAsync(ct);
                response = new AddMemberResponse(
                    member.MemberId, user.UserEmail, member.MemberFirstName, member.MemberLastName,
                    IsNewAccount: false, IsPrimary: isPrimary,
                    member.MemberPhone, member.MemberGender, member.MemberHeightCm, member.MemberBirthDate);
            }

            await tx.CommitAsync(ct);
        }

        await SendMemberAddedMailAsync(coachId, response);
        return response;
    }

    /// <summary>
    /// Best effort: a failure here never changes the 201. One warning without the mail body or address.
    /// RoutingEmailSender applies TEST/PRODUCTION routing.
    /// </summary>
    private async Task SendMemberAddedMailAsync(Guid coachId, AddMemberResponse member)
    {
        try
        {
            var coach = await db.Coaches.AsNoTracking()
                .Where(c => c.CoachId == coachId)
                .Select(c => new { c.CoachFirstName, c.CoachLastName })
                .FirstOrDefaultAsync(CancellationToken.None);

            var subject = MemberAddedEmailBuilder.BuildSubject(AuthService.SanitizeFirstName(coach?.CoachFirstName));
            var fullName = string.Join(" ", new[]
                {
                    MemberAddedEmailBuilder.CleanName(coach?.CoachFirstName),
                    MemberAddedEmailBuilder.CleanName(coach?.CoachLastName)
                }.Where(n => n != null));
            if (fullName.Length == 0)
                fullName = "Votre coach";

            var installUrl = inviteOptions.Value.HasInstallUrl ? inviteOptions.Value.InstallUrl!.Trim() : null;

            var sent = await emailSender.SendAsync(
                EmailMessage.ForRecipient(
                    member.Email,
                    subject,
                    MemberAddedEmailBuilder.BuildHtml(fullName, member.Email, installUrl),
                    MemberAddedEmailBuilder.BuildText(fullName, member.Email, installUrl)),
                CancellationToken.None);

            if (!sent)
                logger.LogWarning("Member added mail was not sent. MemberId={MemberId} CoachId={CoachId}",
                    member.MemberId, coachId);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Member added mail failed ({ExceptionType}). MemberId={MemberId} CoachId={CoachId}",
                ex.GetType().Name, member.MemberId, coachId);
        }
    }

    private static ConflictException Conflict(string reason, string message)
        => new(message, new { reason });
}
