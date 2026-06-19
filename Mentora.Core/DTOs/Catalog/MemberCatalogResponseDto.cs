using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Catalog;

public sealed record MemberCatalogResponseDto(
    CatalogCoachDto Coach,
    IReadOnlyList<ProductCatalogDto> Products,
    IReadOnlyList<PackCatalogDto> Packs
);

public sealed record CatalogCoachDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Bio,
    string? AvatarUrl
);

public sealed record ProductCatalogDto(
    Guid Id,
    string Name,
    string? Description,
    decimal PriceEuros,
    OfferType OfferType,
    int DurationMinutes,
    Guid OfferProgramId,
    string OfferProgramName,
    int? MaxParticipants
);

public sealed record PackCatalogDto(
    Guid Id,
    string Name,
    string? Description,
    decimal PriceEuros,
    IReadOnlyList<PackItemCatalogDto> Items
);

public sealed record PackItemCatalogDto(
    Guid PackItemId,
    int Quantity,
    ProductCatalogDto Product
);
