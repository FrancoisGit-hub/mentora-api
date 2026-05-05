namespace Mentora.Core.DTOs.Catalog;

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
