namespace Mentora.Core.DTOs.Member;

/// <summary>Summary of a member order as returned by the paginated order list endpoint.</summary>
/// <param name="OrderId">Unique identifier of the order.</param>
/// <param name="Status">Current order lifecycle status: PENDING, PAID, EXPIRED, or FAILED.</param>
/// <param name="TotalEuros">Order grand total in euros.</param>
/// <param name="ItemCount">Number of distinct order-item lines in this order (not the sum of quantities).</param>
/// <param name="CreatedDate">Date the order was created (UTC).</param>
/// <param name="PaidAt">Date the payment was confirmed (UTC), or null when not yet paid.</param>
public record OrderSummaryDto(
    Guid OrderId,
    string Status,
    decimal TotalEuros,
    int ItemCount,
    DateTime CreatedDate,
    DateTime? PaidAt);
