namespace Mentora.Core.DTOs.Sessions;

public record SessionDetailResponse(Guid SessionId, string Status, int CreditsConsumed, string Type, DateTime StartDate, DateTime EndDate, string? CancelReason, DateTime? CancelledAt, DateTime BookedAt, string CoachFirstName, string CoachLastName);
