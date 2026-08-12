namespace Mentora.Core.DTOs.Program;

/// <param name="Date">
/// Computed: startDate + (weekNumber - 1 + weekOffset) * 7 + (dayOfWeek - 1), where weekNumber
/// comes from the parent MICROCYCLE block. Nothing is stored — recomputed on every read.
/// When the session is later linked to a booked SESSIONS row (Lot 6.5), that row's own date
/// becomes authoritative instead; that path is not implemented yet.
/// </param>
public record ProgramSessionResponse(
    Guid ProgramSessionId,
    string Name,
    string Type,
    int DayOfWeek,
    int Position,
    string Status,
    DateOnly Date,
    DateTime? CompletedDate,
    string? MemberFeedback,
    string? CoachNote,
    List<ProgramCircuitResponse> Circuits
);
