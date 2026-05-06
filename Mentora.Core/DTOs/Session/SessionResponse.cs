namespace Mentora.Core.DTOs.Session;

public record SessionResponse(
    Guid SessionId,
    Guid VoucherId,
    Guid SlotId,
    Guid CoachId,
    string CoachDisplayName,
    Guid ProductId,
    string ProductName,
    string OfferType,
    int DurationMinutes,
    string Sport,
    DateTime ScheduledAt,
    string Status,
    string? VisioUrl,
    string? CancellationReason,
    string? CancelledBy,
    DateTime? CancelledAt,
    DateTime CreatedDate,
    DateTime UpdatedDate);
