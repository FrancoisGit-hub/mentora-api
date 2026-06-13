namespace Mentora.Core.DTOs.Member;

/// <summary>
/// A group of session vouchers for a specific coach, as returned by
/// <c>GET /api/v1/member/vouchers</c>. Groups are ordered by the member's
/// coach relationship start date (oldest first).
/// </summary>
/// <param name="CoachId">Unique identifier of the coach.</param>
/// <param name="Vouchers">
/// Vouchers for this coach matching the requested status filter,
/// sorted by VOUCHER_UPDATED_DATE descending.
/// </param>
public record MemberVouchersByCoachGroupDto(
    Guid CoachId,
    IReadOnlyList<MemberVoucherDto> Vouchers);
