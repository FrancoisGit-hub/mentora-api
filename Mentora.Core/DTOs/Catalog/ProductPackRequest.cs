namespace Mentora.Core.DTOs.Catalog;

/// <summary>Payload for creating or fully replacing a product pack in the coach's catalog.</summary>
/// <param name="Name">Display name of the pack. Required; max 120 characters.</param>
/// <param name="Description">Optional marketing description. Max 2 000 characters.</param>
/// <param name="PriceEuros">
/// Bundle price in euros, set commercially by the coach. Not computed from
/// the constituent products — any price is accepted as long as it is ≥ 0.
/// </param>
/// <param name="DiscountPercent">
/// Percentage discount applied to this pack at checkout. Must be between 0 and 100 inclusive.
/// Use 0 for no discount. No business guardrail enforced in V1 — the coach owns the value.
/// </param>
/// <param name="Items">
/// Ordered list of products and quantities that make up this pack.
/// At least one item is required; each referenced product must be a published
/// product owned by the same coach.
/// </param>
public record ProductPackRequest(
    string Name,
    string? Description,
    decimal PriceEuros,
    decimal DiscountPercent,
    List<ProductPackItemRequest> Items
);
