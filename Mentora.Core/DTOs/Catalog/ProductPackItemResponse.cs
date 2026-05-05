namespace Mentora.Core.DTOs.Catalog;

public record ProductPackItemResponse(
    Guid ProductPackItemId,
    Guid ProductId,
    string ProductName,
    int Quantity
);
