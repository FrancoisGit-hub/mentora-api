namespace Mentora.Core.DTOs.Catalog;

public record ProductRequest(
    string Name,
    string? Description,
    string OfferType,       // wire: VISIO | PRESENTIEL_SOLO
    string? OfferNature,    // free text, max 80; defaults to CLASSIQUE
    int DurationMinutes,
    decimal PriceEuros,
    string Sport,           // wire: TRAINING | BOXE | SALLE | COURSE
    string? Location,
    List<string>? Tags,
    Guid OfferProgramId
);
