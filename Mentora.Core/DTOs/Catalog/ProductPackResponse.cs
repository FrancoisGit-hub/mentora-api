namespace Mentora.Core.DTOs.Catalog;

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
