namespace Mentora.Core.DTOs.Credits;

public record PurchaseCreditsResponse(int QuantityPurchased, decimal TotalPaidEuros, int NewBalance);
