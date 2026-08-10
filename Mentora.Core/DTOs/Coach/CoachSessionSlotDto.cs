namespace Mentora.Core.DTOs.Coach;

/// <summary>A session slot in the coach's own availability calendar.</summary>
/// <param name="SlotId">Unique identifier of the slot.</param>
/// <param name="StartDate">Slot start date and time (UTC).</param>
/// <param name="EndDate">Slot end date and time (UTC).</param>
/// <param name="OfferType">Session format: VISIO or PRESENTIEL_SOLO.</param>
/// <param name="DurationMinutes">Slot duration in minutes.</param>
/// <param name="IsAvailable">Whether the slot is still open for booking.</param>
/// <param name="SessionId">Identifier of the booking session, or null when the slot is free.</param>
/// <param name="MemberId">Identifier of the booked member, or null when the slot is free.</param>
/// <param name="MemberFirstName">First name of the booked member, or null when the slot is free.</param>
/// <param name="MemberLastName">Last name of the booked member, or null when the slot is free.</param>
public record CoachSessionSlotDto(
    Guid SlotId,
    DateTime StartDate,
    DateTime EndDate,
    string OfferType,
    int DurationMinutes,
    bool IsAvailable,
    Guid? SessionId,
    Guid? MemberId,
    string? MemberFirstName,
    string? MemberLastName);
