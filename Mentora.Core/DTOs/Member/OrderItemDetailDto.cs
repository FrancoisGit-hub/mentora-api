namespace Mentora.Core.DTOs.Member;

/// <summary>A single order-item line as returned by the order detail endpoint.</summary>
/// <param name="OrderItemId">Unique identifier of the order item.</param>
/// <param name="ProductId">Identifier of the product purchased (weak reference; product may be archived).</param>
/// <param name="ProductName">Product name as captured at order time (immutable snapshot).</param>
/// <param name="PackId">Identifier of the pack this item originated from, or <c>null</c> for individual products.</param>
/// <param name="Quantity">Number of session vouchers included on this line.</param>
/// <param name="OfferType">Session format: VISIO or PRESENTIEL_SOLO.</param>
/// <param name="DurationMinutes">Session duration in minutes, snapshotted at purchase time.</param>
/// <param name="Sport">Sport discipline, snapshotted at purchase time.</param>
/// <param name="OriginalUnitPriceEuros">Unit price before any pack discount.</param>
/// <param name="UnitPriceEuros">Unit price after any pack discount.</param>
/// <param name="PackDiscountPercentApplied">Pack discount percentage applied to this line, or <c>null</c> for individual products.</param>
/// <param name="LineTotalEuros">Quantity × unit price after discount.</param>
public record OrderItemDetailDto(
    Guid OrderItemId,
    Guid ProductId,
    string ProductName,
    Guid? PackId,
    int Quantity,
    string OfferType,
    int DurationMinutes,
    string Sport,
    decimal OriginalUnitPriceEuros,
    decimal UnitPriceEuros,
    decimal? PackDiscountPercentApplied,
    decimal LineTotalEuros);
