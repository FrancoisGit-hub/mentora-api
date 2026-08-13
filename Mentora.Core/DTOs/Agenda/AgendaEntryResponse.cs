namespace Mentora.Core.DTOs.Agenda;

/// <param name="Date">
/// AUTHORITY RULE: for a booked entry, this is SESSIONS.SESSION_SCHEDULED_AT — never the computed
/// formula. For a standalone (unbooked) program session, this is the computed date (SKIP: the raw
/// formula; SHIFT: with the read-time shift applied), at midnight UTC since a program session
/// carries no time of day.
/// </param>
/// <param name="Type">Wire session/program type, e.g. PRESENTIEL_SOLO.</param>
/// <param name="Title">Member's name for an individual booking; "Cours collectif (N participants)" for a group booking; the program session's own name when standalone.</param>
/// <param name="Status">The booked session's effective status, or the program session's status when standalone.</param>
/// <param name="IsBooked">True when this entry is a SESSIONS row; false for a standalone program session.</param>
/// <param name="ProgramSessionId">
/// The linked program session, when there is exactly one to show: always populated for an
/// individual booking or a standalone entry. On the member's own agenda, also populated for a
/// group booking (their own linked program session). On the coach's agenda, left null for a group
/// booking — one booking can carry several participants' program sessions, which a single scalar
/// field cannot represent; see ParticipantCount instead.
/// </param>
/// <param name="SessionId">The linked booking, when this entry is booked.</param>
/// <param name="ParticipantCount">Group bookings only: the number of active (non-cancelled) participants.</param>
/// <param name="IsOverdue">Standalone entries only: true when the computed date is in the past and the status is still PLANNED.</param>
public record AgendaEntryResponse(
    DateTime Date,
    string Type,
    string Title,
    string Status,
    bool IsBooked,
    Guid? ProgramSessionId,
    Guid? SessionId,
    int? ParticipantCount,
    bool IsOverdue);
