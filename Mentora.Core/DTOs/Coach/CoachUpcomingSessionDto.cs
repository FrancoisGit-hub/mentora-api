namespace Mentora.Core.DTOs.Coach;

/// <summary>A session scheduled for the coach within the next 7 days.</summary>
/// <param name="SessionId">Unique identifier of the session.</param>
/// <param name="MemberId">Identifier of the member the session is booked for.</param>
/// <param name="MemberFirstName">First name of the member.</param>
/// <param name="MemberLastName">Last name of the member.</param>
/// <param name="ScheduledAt">Date and time the session is scheduled for (UTC).</param>
/// <param name="DurationMinutes">Session duration in minutes.</param>
/// <param name="OfferType">Session format: VISIO or PRESENTIEL_SOLO.</param>
/// <param name="VisioUrl">Video call URL, present only for VISIO sessions.</param>
public record CoachUpcomingSessionDto(
    Guid SessionId,
    Guid MemberId,
    string MemberFirstName,
    string MemberLastName,
    DateTime ScheduledAt,
    int DurationMinutes,
    string OfferType,
    string? VisioUrl);
