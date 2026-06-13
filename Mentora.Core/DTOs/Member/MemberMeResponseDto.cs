namespace Mentora.Core.DTOs.Member;

/// <summary>Full mobile home-screen payload: authenticated member's profile and vouchers grouped by coach.</summary>
/// <param name="Profile">The member's profile data.</param>
/// <param name="Coaches">
/// Coaches linked to this member via MEMBER_COACHES, ordered: primary coach first,
/// then by MEMBER_COACHES.STARTED_AT ascending. Each group contains the coach summary
/// and the filtered vouchers for that coach.
/// </param>
public record MemberMeResponseDto(
    MemberProfileDto Profile,
    IReadOnlyList<MemberCoachGroupDto> Coaches);
