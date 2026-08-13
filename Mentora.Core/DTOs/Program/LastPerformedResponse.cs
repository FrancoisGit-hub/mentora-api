namespace Mentora.Core.DTOs.Program;

/// <summary>
/// The most recent DONE record of this same EXERCISE_ID by this member, across any program,
/// excluding the session currently being displayed. Null when there is no prior record.
/// </summary>
/// <param name="Date">The recording session's PROGRAM_SESSION_COMPLETED_DATE.</param>
public record LastPerformedResponse(
    DateTime Date,
    int? ActualSets,
    int? ActualReps,
    decimal? ActualWeightKg,
    int? ActualRpe);
