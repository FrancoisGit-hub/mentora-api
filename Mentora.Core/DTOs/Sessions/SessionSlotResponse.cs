namespace Mentora.Core.DTOs.Sessions;

public record SessionSlotResponse(Guid SessionSlotId, DateTime StartDate, DateTime EndDate, int CreditsRequired, string Type, bool IsAvailable);
