namespace Mentora.Core.DTOs.Program;

/// <summary>
/// Full replacement of a program session's completion record — resubmitting corrects a previous
/// entry. Never a partial patch: any ProgramExerciseId belonging to this session but omitted from
/// <see cref="Exercises"/> has its actuals (sets/reps/weight/RPE/feedback) cleared, not left alone.
/// </summary>
/// <param name="Status">PLANNED, DONE, or SKIPPED.</param>
/// <param name="MemberFeedback">Session-level feedback, distinct from each exercise's own.</param>
/// <param name="CoachNote">
/// Coach-only — writes PROGRAM_SESSION_COACH_NOTE. Present on the shared wire shape so both
/// endpoints accept the same body, but only applied when the caller is the coach; a member
/// supplying it has it silently ignored, never persisted.
/// </param>
/// <param name="Exercises">Full replacement set — see summary.</param>
public record UpdateProgramSessionCompletionRequest(
    string Status,
    string? MemberFeedback,
    string? CoachNote,
    List<ProgramExerciseActualRequest> Exercises);
