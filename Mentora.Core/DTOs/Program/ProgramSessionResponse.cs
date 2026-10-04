namespace Mentora.Core.DTOs.Program;

/// <param name="Date">
/// Computed: startDate + (weekNumber - 1 + weekOffset) * 7 + (dayOfWeek - 1), where weekNumber
/// comes from the parent MICROCYCLE block. Nothing is stored — recomputed on every read.
/// AUTHORITY RULE: once this session is linked to a booked SESSIONS row, that row's own
/// SESSION_SCHEDULED_AT becomes authoritative instead, and this formula is never evaluated for it.
/// </param>
/// <param name="Nature">STANDARD | EVALUATION.</param>
/// <param name="SessionId">Linked booking (SESSIONS row) id, null when not linked.</param>
public record ProgramSessionResponse(
    Guid ProgramSessionId,
    string Name,
    string Type,
    string Nature,
    int DayOfWeek,
    int Position,
    string Status,
    DateOnly Date,
    DateTime? CompletedDate,
    string? MemberFeedback,
    string? CoachNote,
    Guid? SessionId,
    List<ProgramCircuitResponse> Circuits
);
