namespace Mentora.Core.DTOs.Cart;

public record CheckoutResponse(
    Guid OrderId,
    string CheckoutUrl,
    decimal TotalEuros);
