namespace Mentora.Core.DTOs.Catalog;

/// <summary>Payload for creating or fully replacing a product in the coach's catalog.</summary>
/// <param name="Name">Display name of the product. Required; max 120 characters.</param>
/// <param name="Description">Optional marketing description. Max 2 000 characters.</param>
/// <param name="OfferType">
/// Delivery format. Accepted values: <c>VISIO</c> (remote video session),
/// <c>PRESENTIEL_SOLO</c> (in-person one-to-one), or <c>PRESENTIEL_GROUPE</c> (in-person
/// group session — required for group-session products). <c>VISIO_GROUPE</c> is reserved
/// for V2 and is currently rejected by validation.
/// </param>
/// <param name="OfferNature">
/// Free-text label describing the offer nature (e.g. <c>CLASSIQUE</c>, <c>PREMIUM</c>).
/// Max 80 characters. Defaults to <c>CLASSIQUE</c> when omitted or null.
/// </param>
/// <param name="DurationMinutes">Duration of the session in minutes. Must be a positive integer.</param>
/// <param name="PriceEuros">Unit price in euros. Must be ≥ 0.</param>
/// <param name="Sport">
/// Sport or discipline category. Accepted values:
/// <c>TRAINING</c>, <c>BOXE</c>, <c>SALLE</c>, <c>COURSE</c>.
/// </param>
/// <param name="Location">Optional physical address or venue name for in-person sessions.</param>
/// <param name="Tags">
/// Freeform classification labels. Deduplicated case-insensitively at save time.
/// Max 20 tags; each tag between 1 and 30 characters.
/// </param>
/// <param name="OfferProgramId">
/// The program this product belongs to. Must be a valid, active <c>OfferProgram</c>
/// owned by the authenticated coach; rejected with 400 otherwise.
/// </param>
/// <param name="MaxParticipants">
/// Capacity for group offer types (<c>PRESENTIEL_GROUPE</c>). <c>null</c> means no limit. Not
/// enforced at the database level; only meaningful for group offer types, but not validated as
/// such — a solo product with a value here is simply ignored downstream.
/// </param>
public record ProductRequest(
    string Name,
    string? Description,
    string OfferType,
    string? OfferNature,
    int DurationMinutes,
    decimal PriceEuros,
    string Sport,
    string? Location,
    List<string>? Tags,
    Guid OfferProgramId,
    int? MaxParticipants = null
);
