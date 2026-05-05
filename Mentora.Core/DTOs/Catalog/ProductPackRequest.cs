namespace Mentora.Core.DTOs.Catalog;

public record ProductPackRequest(
    string Name,
    string? Description,
    decimal PriceEuros,
    List<ProductPackItemRequest> Items
);
