namespace Mentora.Core.DTOs.Sessions;

public record CreateSessionSlotRequest(DateTime StartDate, DateTime EndDate, decimal PriceEuros, string Type);
