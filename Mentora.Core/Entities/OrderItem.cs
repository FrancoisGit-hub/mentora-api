using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class OrderItem
{
    public Guid OrderItemId { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    // Weak references (no FK constraints) — snapshot semantics
    public Guid ProductId { get; set; }
    public Guid? PackId { get; set; }

    // Snapshot fields — immutable after order creation
    public string ProductName { get; set; } = null!;
    public decimal UnitPriceEuros { get; set; }
    public decimal OrderItemOriginalUnitPriceEuros { get; set; }
    public decimal? OrderItemPackDiscountPercentApplied { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotalEuros { get; set; }
    public OfferType OfferType { get; set; }
    public int DurationMinutes { get; set; }
    public Sport Sport { get; set; } = Sport.Training;
}
