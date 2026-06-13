namespace Mentora.Core.DTOs.Member;

/// <summary>Full order detail as returned by <c>GET /api/v1/member/orders/{orderId}</c>.</summary>
/// <param name="OrderId">Unique identifier of the order.</param>
/// <param name="CoachId">Identifier of the coach the order is with.</param>
/// <param name="Status">Current lifecycle status: PENDING, PAID, EXPIRED, or FAILED.</param>
/// <param name="TotalEuros">Order grand total in euros.</param>
/// <param name="CreatedDate">Date the order was created (UTC).</param>
/// <param name="UpdatedDate">Date the order was last updated (UTC).</param>
/// <param name="PaidAt">Date the payment was confirmed (UTC), or <c>null</c> when not yet paid.</param>
/// <param name="StripeSessionId">Stripe checkout session identifier, or <c>null</c> for non-Stripe orders.</param>
/// <param name="StripeCheckoutUrl">URL used to redirect the member to the Stripe checkout page, or <c>null</c>.</param>
/// <param name="Items">Order line items.</param>
/// <param name="Vouchers">
/// Session vouchers generated for this order.
/// Empty when the order is not yet PAID (vouchers are created by the Stripe webhook on payment confirmation).
/// Sorted by creation date ascending (natural generation order from the webhook).
/// </param>
public record OrderDetailDto(
    Guid OrderId,
    Guid CoachId,
    string Status,
    decimal TotalEuros,
    DateTime CreatedDate,
    DateTime UpdatedDate,
    DateTime? PaidAt,
    string? StripeSessionId,
    string? StripeCheckoutUrl,
    IReadOnlyList<OrderItemDetailDto> Items,
    IReadOnlyList<OrderVoucherDto> Vouchers);
