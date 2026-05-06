namespace Mentora.Core.DTOs.Catalog;

/// <summary>A product line within a pack as returned by the API.</summary>
/// <param name="ProductPackItemId">Unique identifier of this pack item row.</param>
/// <param name="ProductId">Identifier of the product included in this line.</param>
/// <param name="ProductName">Display name of the product at the time the pack was last saved.</param>
/// <param name="Quantity">Number of sessions of this product included in the pack.</param>
public record ProductPackItemResponse(
    Guid ProductPackItemId,
    Guid ProductId,
    string ProductName,
    int Quantity
);
