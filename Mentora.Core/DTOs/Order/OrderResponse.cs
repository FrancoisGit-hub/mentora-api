namespace Mentora.Core.DTOs.Order;

public record OrderResponse(
    Guid OrderId,
    Guid CoachId,
    string CoachDisplayName,
    string Status,
    decimal TotalEuros,
    string? StripeCheckoutUrl,
    DateTime CreatedDate,
    DateTime UpdatedDate,
    DateTime? PaidAt,
    IReadOnlyList<OrderItemResponse> Items);
