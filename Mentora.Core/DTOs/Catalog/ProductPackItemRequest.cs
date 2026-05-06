namespace Mentora.Core.DTOs.Catalog;

/// <summary>A product line within a pack creation or update request.</summary>
/// <param name="ProductId">
/// Identifier of an existing, published product owned by the same coach.
/// Referencing a product in DRAFT or ARCHIVED status is rejected with 400.
/// </param>
/// <param name="Quantity">
/// Number of sessions of this product included in the pack. Must be ≥ 1.
/// </param>
public record ProductPackItemRequest(
    Guid ProductId,
    int Quantity
);
