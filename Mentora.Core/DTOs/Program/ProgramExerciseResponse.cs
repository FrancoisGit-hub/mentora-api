using Mentora.Core.DTOs.Exercise;

namespace Mentora.Core.DTOs.Program;

/// <summary>
/// A prescribed exercise within a program circuit. Only <see cref="ProgramExerciseId"/> and the
/// prescribed/actual/position/loadType/customNote fields are stored per-program; the exercise
/// details (name, instructions, media, muscleGroup, equipment) are resolved live from EXERCISES.
/// <see cref="Video"/> (Lot 12.3a, last key "video"): the MUX video of the exercise, only when READY
/// with a playback id, else null (same rule as the exercise detail).
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
    int? RestSecondsAfterSeries,
    int? WorkSeconds,
    int? RestWorkSeconds,
    int? ActualSets,
    int? ActualReps,
    decimal? ActualWeightKg,
    int? ActualRpe,
    string? MemberFeedback,
    LastPerformedResponse? LastPerformed,
    ExerciseVideoResponse? Video
);
