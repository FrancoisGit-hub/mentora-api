using Mentora.Core.DTOs.Coach;

namespace Mentora.Core.Interfaces;

/// <summary>
/// Lets a coach view and edit their per-member override settings (currently just the
/// presential-address override). Every method requires a MEMBER_COACHES row linking the
/// coach to the member — a missing link throws <see cref="Exceptions.NotFoundException"/>
/// (404), never a forbidden response, so a coach can't distinguish "this member doesn't
/// exist" from "this member exists but isn't yours."
/// </summary>
public interface ICoachMemberSettingsService
{
    Task<MemberSettingsDto> GetAsync(Guid coachId, Guid memberId, CancellationToken ct);
    Task<MemberSettingsDto> UpdateAsync(Guid coachId, Guid memberId, UpdateMemberSettingsRequest request, CancellationToken ct);
}
