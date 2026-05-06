namespace Mentora.Core.DTOs.Catalog;

/// <summary>Representation of a product returned by the API.</summary>
/// <param name="ProductId">Unique identifier of the product.</param>
/// <param name="Name">Display name of the product.</param>
/// <param name="Description">Optional marketing description.</param>
/// <param name="OfferType">Delivery format: <c>VISIO</c> or <c>PRESENTIEL_SOLO</c>.</param>
/// <param name="OfferNature">Free-text offer nature label (e.g. <c>CLASSIQUE</c>).</param>
/// <param name="DurationMinutes">Duration of the session in minutes.</param>
/// <param name="PriceEuros">Unit price in euros.</param>
/// <param name="Sport">Sport or discipline category.</param>
/// <param name="Location">Physical address or venue name, if applicable.</param>
/// <param name="Tags">Classification labels attached to this product.</param>
/// <param name="Status">
/// Lifecycle status of the product: <c>DRAFT</c>, <c>PUBLISHED</c>, or <c>ARCHIVED</c>.
/// </param>
/// <param name="OfferProgramId">Identifier of the program this product belongs to.</param>
/// <param name="CoachId">Identifier of the coach who owns this product.</param>
/// <param name="CreatedDate">Timestamp when the product was created (UTC).</param>
/// <param name="UpdatedDate">Timestamp of the last modification (UTC).</param>
public record ProductResponse(
    Guid ProductId,
    string Name,
    string? Description,
    string OfferType,
    string OfferNature,
    int DurationMinutes,
    decimal PriceEuros,
    string Sport,
    string? Location,
    List<string>? Tags,
    string Status,
    Guid OfferProgramId,
    Guid CoachId,
    DateTime CreatedDate,
    DateTime UpdatedDate
);
