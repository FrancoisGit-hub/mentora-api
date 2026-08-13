namespace Mentora.Core.DTOs.Coach;

/// <summary>A session scheduled for the coach within the next 7 days — individual or group.</summary>
/// <param name="SessionId">Unique identifier of the session.</param>
/// <param name="MemberId">Identifier of the member the session is booked for, or null for a group session.</param>
/// <param name="MemberFirstName">First name of the member, or null for a group session.</param>
/// <param name="MemberLastName">Last name of the member, or null for a group session.</param>
/// <param name="Title">The member's name for an individual session, or "Cours collectif (N participants)" for a group one.</param>
/// <param name="ParticipantCount">Group sessions only: the number of active (non-cancelled) participants. Null for an individual session.</param>
/// <param name="ScheduledAt">Date and time the session is scheduled for (UTC).</param>
/// <param name="DurationMinutes">Session duration in minutes.</param>
/// <param name="OfferType">Session format: VISIO, PRESENTIEL_SOLO, or PRESENTIEL_GROUPE.</param>
/// <param name="VisioUrl">Video call URL, present only for VISIO sessions.</param>
public record CoachUpcomingSessionDto(
    Guid SessionId,
    Guid? MemberId,
    string? MemberFirstName,
    string? MemberLastName,
    string Title,
    int? ParticipantCount,
    DateTime ScheduledAt,
    int DurationMinutes,
    string OfferType,
    string? VisioUrl);
