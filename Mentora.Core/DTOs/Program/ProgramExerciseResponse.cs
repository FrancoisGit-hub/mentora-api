namespace Mentora.Core.DTOs.Program;

/// <summary>
/// A prescribed exercise within a program circuit. Only <see cref="ProgramExerciseId"/> and the
/// prescribed/actual/position/loadType/customNote fields are stored per-program; the exercise
/// details (name, instructions, media, muscleGroup, equipment) are resolved live from EXERCISES.
/// </summary>
public record ProgramExerciseResponse(
    Guid ProgramExerciseId,
    Guid ExerciseId,
    string ExerciseName,
    string? ExerciseInstructions,
    string? ExerciseVideoUrl,
    string? ExerciseImageUrl,
    string ExerciseMuscleGroup,
    string ExerciseEquipment,
    int Position,
    string LoadType,
    string? CustomNote,
    int? PrescribedSets,
    int? PrescribedReps,
    decimal? PrescribedWeightKg,
    int? RestSeconds,
    int? WorkSeconds,
    int? RestWorkSeconds,
    int? ActualSets,
    int? ActualReps,
    decimal? ActualWeightKg,
    int? ActualRpe,
    string? MemberFeedback
);
