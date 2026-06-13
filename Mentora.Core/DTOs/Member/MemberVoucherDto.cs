namespace Mentora.Core.DTOs.Member;

/// <summary>A single session voucher as seen from the member's perspective.</summary>
/// <param name="VoucherId">Unique identifier of the voucher.</param>
/// <param name="Status">Current lifecycle status: AVAILABLE, RESERVED, or CONSUMED.</param>
/// <param name="OfferType">Session format: VISIO or PRESENTIEL_SOLO.</param>
/// <param name="DurationMinutes">Session duration in minutes, snapshotted at purchase time.</param>
/// <param name="Sport">Sport discipline, snapshotted at purchase time.</param>
/// <param name="ProductId">Identifier of the product this voucher was generated from (weak reference — product may be archived).</param>
/// <param name="ProductName">Product name as captured at order time (immutable snapshot from ORDER_ITEMS).</param>
/// <param name="ReservedSessionId">Identifier of the reserved session, or null when the voucher is AVAILABLE or CONSUMED.</param>
/// <param name="CreatedDate">Date the voucher was created (UTC).</param>
/// <param name="UpdatedDate">Date the voucher was last updated (UTC). Used for CONSUMED-since filtering.</param>
public record MemberVoucherDto(
    Guid VoucherId,
    string Status,
    string OfferType,
    int DurationMinutes,
    string Sport,
    Guid ProductId,
    string ProductName,
    Guid? ReservedSessionId,
    DateTime CreatedDate,
    DateTime UpdatedDate);
