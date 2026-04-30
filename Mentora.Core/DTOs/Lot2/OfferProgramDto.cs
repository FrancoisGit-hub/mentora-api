namespace Mentora.Core.DTOs.Lot2;

public record OfferProgramDto(
    Guid Id,
    string Name,
    bool IsActive,
    DateTime CreatedAt
);
