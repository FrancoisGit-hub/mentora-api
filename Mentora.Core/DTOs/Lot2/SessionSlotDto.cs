namespace Mentora.Core.DTOs.Lot2;

public record SessionSlotDto(
    Guid Id,
    DateTime StartDate,
    DateTime EndDate,
    string OfferType,
    int DurationMinutes,
    bool IsAvailable,
    DateTime CreatedAt
);
