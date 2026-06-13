namespace Mentora.Core.DTOs.Member;

/// <summary>
/// A coach group: one coach paired with all vouchers the member holds for that coach.
/// The <see cref="IsPrimary"/> flag mirrors MEMBER_COACHES.IS_PRIMARY.
/// </summary>
/// <param name="Coach">Summary of the coach (billing parameters included).</param>
/// <param name="IsPrimary">True if this is the member's primary coach.</param>
/// <param name="Vouchers">
/// Vouchers for this coach, sorted by status priority (RESERVED → AVAILABLE → CONSUMED)
/// then by VOUCHER_UPDATED_DATE descending within each status.
/// Empty when the member holds no vouchers for this coach.
/// </param>
public record MemberCoachGroupDto(
    MemberCoachSummaryDto Coach,
    bool IsPrimary,
    IReadOnlyList<MemberVoucherDto> Vouchers);
