using Mentora.Core.DTOs.Coach;

namespace Mentora.Core.Interfaces;

public interface ICoachMemberInviteService
{
    /// <summary>
    /// Lot 14.1: attaches a member (identified by email) to the coach, creating the account when the
    /// email is unknown. One transaction under an advisory lock; a best-effort mail follows the commit.
    /// Throws ConflictException (ACCOUNT_DISABLED, EMAIL_IS_COACH, ALREADY_ATTACHED) or ValidationException.
    /// </summary>
    Task<AddMemberResponse> AddMemberAsync(Guid coachId, AddMemberRequest request, CancellationToken ct);
}
