namespace Mentora.Core.DTOs.Cart;

public record CartItemResponse(
    Guid CartItemId,
    Guid? ProductId,
    Guid? PackId,
    string ItemName,
    string ItemKind,
    decimal UnitPriceEuros,
    int Quantity,
    decimal LineTotalEuros,
    DateTime AddedDate);
