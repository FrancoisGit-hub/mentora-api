namespace Mentora.Core.DTOs.Catalog;

public record ProductPackItemRequest(
    Guid ProductId,
    int Quantity
);
