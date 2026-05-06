namespace Mentora.Core.DTOs.Order;

public record OrderItemResponse(
    Guid OrderItemId,
    Guid ProductId,
    Guid? PackId,
    string ProductName,
    decimal OriginalUnitPriceEuros,
    decimal UnitPriceEuros,
    decimal? PackDiscountPercentApplied,
    int Quantity,
    decimal LineTotalEuros,
    string OfferType,
    int DurationMinutes,
    string Sport);
