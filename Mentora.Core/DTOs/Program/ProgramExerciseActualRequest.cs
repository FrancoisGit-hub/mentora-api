namespace Mentora.Core.DTOs.Program;

/// <param name="ProgramExerciseId">Must belong to the program session being completed, else 422.</param>
/// <param name="ActualWeightKg">Only allowed when the exercise's LoadType is KG; on a BODYWEIGHT exercise this returns 422.</param>
/// <param name="ActualRpe">1..10 when present, else 422.</param>
public record ProgramExerciseActualRequest(
    Guid ProgramExerciseId,
    int? ActualSets,
    int? ActualReps,
    decimal? ActualWeightKg,
    int? ActualRpe,
    string? MemberFeedback);
