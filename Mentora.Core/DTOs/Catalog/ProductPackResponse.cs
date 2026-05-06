namespace Mentora.Core.DTOs.Catalog;

/// <summary>Representation of a product pack returned by the API.</summary>
/// <param name="ProductPackId">Unique identifier of the product pack.</param>
/// <param name="Name">Display name of the product pack.</param>
/// <param name="Description">Optional marketing description.</param>
/// <param name="PriceEuros">
/// Bundle price in euros as set by the coach. This is a commercial price and is
/// not constrained by the sum of the individual product prices — treat it as
/// informative only when comparing against item totals.
/// </param>
/// <param name="Status">
/// Lifecycle status of the pack: <c>DRAFT</c>, <c>PUBLISHED</c>, or <c>ARCHIVED</c>.
/// </param>
/// <param name="Items">Products and quantities that make up this pack.</param>
/// <param name="CoachId">Identifier of the coach who owns this pack.</param>
/// <param name="CreatedDate">Timestamp when the pack was created (UTC).</param>
/// <param name="UpdatedDate">Timestamp of the last modification (UTC).</param>
public record ProductPackResponse(
    Guid ProductPackId,
    string Name,
    string? Description,
    decimal PriceEuros,
    string Status,
    List<ProductPackItemResponse> Items,
    Guid CoachId,
    DateTime CreatedDate,
    DateTime UpdatedDate
);
