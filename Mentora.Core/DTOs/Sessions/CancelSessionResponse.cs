namespace Mentora.Core.DTOs.Sessions;

public record CancelSessionResponse(Guid SessionId, string Status, int CreditsRefunded, int RemainingBalance);
