namespace Mentora.Core.DTOs.Sessions;

public record BookSessionResponse(Guid SessionId, int CreditsConsumed, int RemainingBalance, DateTime StartDate, DateTime EndDate, string Type);
