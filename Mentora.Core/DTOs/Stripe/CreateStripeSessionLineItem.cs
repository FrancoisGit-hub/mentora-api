namespace Mentora.Core.DTOs.Stripe;

public record CreateStripeSessionLineItem(
    string ProductName,
    decimal UnitPriceEuros,
    int Quantity);
