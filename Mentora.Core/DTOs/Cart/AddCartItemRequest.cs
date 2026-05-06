namespace Mentora.Core.DTOs.Cart;

public record AddCartItemRequest(
    Guid? ProductId,
    Guid? PackId,
    int Quantity);
