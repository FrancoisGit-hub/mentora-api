namespace Mentora.Core.DTOs.Member;

/// <summary>
/// A session voucher as seen within a specific order, as returned by
/// <c>GET /api/v1/member/orders/{orderId}</c>.
/// </summary>
/// <remarks>
/// This is intentionally separate from <see cref="MemberVoucherDto"/>.
/// The member-vouchers endpoints group by coach (so the coach is implicit in the parent group),
/// whereas the order detail is centred on one order and includes <see cref="CoachId"/> directly
/// on each voucher for caller convenience.
/// </remarks>
/// <param name="VoucherId">Unique identifier of the voucher.</param>
/// <param name="CoachId">Identifier of the coach the session will be with.</param>
/// <param name="OrderItemId">Identifier of the order-item line that generated this voucher.</param>
/// <param name="ProductId">Identifier of the product (weak reference; may be archived).</param>
/// <param name="ProductName">Product name snapshot from the originating order item.</param>
/// <param name="OfferType">Session format: VISIO or PRESENTIEL_SOLO.</param>
/// <param name="DurationMinutes">Session duration in minutes.</param>
/// <param name="Sport">Sport discipline.</param>
/// <param name="Status">Current lifecycle status: AVAILABLE, RESERVED, or CONSUMED.</param>
/// <param name="ReservedSessionId">Identifier of the reserved session, or <c>null</c> when AVAILABLE or CONSUMED.</param>
/// <param name="CreatedDate">Date the voucher was created (UTC).</param>
/// <param name="UpdatedDate">Date the voucher was last updated (UTC).</param>
public record OrderVoucherDto(
    Guid VoucherId,
    Guid CoachId,
    Guid OrderItemId,
    Guid ProductId,
    string ProductName,
    string OfferType,
    int DurationMinutes,
    string Sport,
    string Status,
    Guid? ReservedSessionId,
    DateTime CreatedDate,
    DateTime UpdatedDate);
