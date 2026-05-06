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
/// <param name="ItemsTotalEuros">
/// Sum of each pack item's current product price multiplied by its quantity.
/// Computed at read time from the live product catalog; not stored in the database.
/// Informative only — the coach is free to set <see cref="PriceEuros"/> independently
/// (e.g. to apply a bundle discount or markup). No validation enforces any relationship
/// between the two values.
/// Items whose referenced product has been archived or is otherwise unavailable
/// contribute 0 to this total.
/// </param>
/// <param name="DiscountPercent">
/// Percentage discount applied to this pack at checkout (0 = no discount, 100 = free).
/// Stored on the pack; coaches set this independently of the constituent product prices.
/// </param>
/// <param name="EffectivePriceEuros">
/// Preview of what a member would pay: <see cref="ItemsTotalEuros"/> × (1 − <see cref="DiscountPercent"/> / 100),
/// ceiling-rounded to the nearest cent. Not stored; computed at read time.
/// Uses a ceiling-to-cent rule so the preview matches the per-line ceiling
/// applied at checkout — the result may be marginally higher than a naive
/// floor or round on the total. Informative only.
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
    decimal DiscountPercent,
    decimal ItemsTotalEuros,
    decimal EffectivePriceEuros,
    string Status,
    List<ProductPackItemResponse> Items,
    Guid CoachId,
    DateTime CreatedDate,
    DateTime UpdatedDate
);
