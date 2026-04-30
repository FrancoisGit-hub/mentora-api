namespace Mentora.Core.DTOs.Lot2;

public record CreateSessionSlotRequest(DateTime StartDate, DateTime EndDate, string OfferType);
