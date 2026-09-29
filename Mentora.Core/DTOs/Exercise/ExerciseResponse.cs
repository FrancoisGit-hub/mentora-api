namespace Mentora.Core.DTOs.Exercise;

/// <summary>Representation of an exercise returned by the API.</summary>
/// <param name="ExerciseId">Unique identifier of the exercise.</param>
/// <param name="CoachId">Owning coach, or <c>null</c> when this is a shared Mentora catalogue entry.</param>
/// <param name="Name">Display name of the exercise.</param>
/// <param name="Description">Optional short description.</param>
/// <param name="Instructions">Optional step-by-step execution instructions.</param>
/// <param name="VideoUrl">Optional demonstration video URL.</param>
/// <param name="ImageUrl">Optional illustration image URL.</param>
/// <param name="MuscleGroup">Deprecated since Lot 9, use primaryMuscleGroup / primaryEquipment. Removed in Lot 10.</param>
/// <param name="Equipment">Deprecated since Lot 9, use primaryMuscleGroup / primaryEquipment. Removed in Lot 10.</param>
/// <param name="IsPolyarticular">Whether the exercise recruits multiple joints/muscle groups at once.</param>
/// <param name="IsActive">Whether the exercise is active (soft-delete flag).</param>
/// <param name="CreatedDate">Timestamp when the exercise was created (UTC).</param>
/// <param name="UpdatedDate">Timestamp of the last modification (UTC).</param>
/// <param name="SourceCode">Code of the exercise in the programming sheet (e.g. <c>EX001</c>), or <c>null</c>.</param>
/// <param name="NameEn">English name, or <c>null</c>.</param>
/// <param name="Category">Catalog category (e.g. <c>Musculation</c>), or <c>null</c>.</param>
/// <param name="MovementPattern">Movement pattern (e.g. <c>Poussée horizontale</c>), or <c>null</c>.</param>
/// <param name="ExerciseType">Exercise type (e.g. <c>Polyarticulaire</c>), or <c>null</c>.</param>
/// <param name="Laterality">Laterality (e.g. <c>Bilatéral</c>), or <c>null</c>.</param>
/// <param name="StartingPosition">Starting position, or <c>null</c>.</param>
/// <param name="GripType">Grip type, or <c>null</c>.</param>
/// <param name="PrimaryMuscleGroup">Primary muscle group, or <c>null</c>.</param>
/// <param name="SecondaryMuscles">Secondary muscles (names); empty when none.</param>
/// <param name="PrimaryEquipment">Primary equipment, or <c>null</c>.</param>
/// <param name="SecondaryEquipment">Secondary equipment, or <c>null</c>.</param>
/// <param name="Level">Minimum level: <c>BEGINNER</c>, <c>INTERMEDIATE</c> or <c>ADVANCED</c>; <c>null</c> when unknown.</param>
/// <param name="Breathing">Breathing advice, or <c>null</c>.</param>
/// <param name="CommonMistakes">Common execution mistakes; empty when none.</param>
/// <param name="Precautions">Precautions, or <c>null</c>.</param>
/// <param name="CoachTip">Coaching tip, or <c>null</c>.</param>
/// <param name="Objectives">Compatible objectives, sorted by name.</param>
/// <param name="Video">Demonstration video, or <c>null</c> when none is attached.</param>
public record ExerciseResponse(
    Guid ExerciseId,
    Guid? CoachId,
    string Name,
    string? Description,
    string? Instructions,
    string? VideoUrl,
    string? ImageUrl,
    string MuscleGroup,
    string Equipment,
    bool IsPolyarticular,
    bool IsActive,
    DateTime CreatedDate,
    DateTime UpdatedDate,
    string? SourceCode,
    string? NameEn,
    string? Category,
    string? MovementPattern,
    string? ExerciseType,
    string? Laterality,
    string? StartingPosition,
    string? GripType,
    ExerciseMuscleGroupRef? PrimaryMuscleGroup,
    IReadOnlyList<string> SecondaryMuscles,
    ExerciseEquipmentRef? PrimaryEquipment,
    ExerciseEquipmentRef? SecondaryEquipment,
    string? Level,
    string? Breathing,
    IReadOnlyList<string> CommonMistakes,
    string? Precautions,
    string? CoachTip,
    IReadOnlyList<ExerciseObjectiveRef> Objectives,
    ExerciseVideoResponse? Video
);

/// <summary>Reference to a muscle group.</summary>
/// <param name="MuscleGroupId">Muscle group identifier.</param>
/// <param name="Name">Muscle group name.</param>
public record ExerciseMuscleGroupRef(Guid MuscleGroupId, string Name);

/// <summary>Reference to a piece of equipment.</summary>
/// <param name="EquipmentId">Equipment identifier.</param>
/// <param name="Name">Equipment name.</param>
public record ExerciseEquipmentRef(Guid EquipmentId, string Name);

/// <summary>Reference to a training objective.</summary>
/// <param name="ObjectiveId">Objective identifier.</param>
/// <param name="Name">Objective name.</param>
public record ExerciseObjectiveRef(Guid ObjectiveId, string Name);

/// <summary>Demonstration video attached to an exercise.</summary>
/// <param name="PlaybackId">Mux playback id, or <c>null</c> while the asset is not ready.</param>
/// <param name="Status"><c>WAITING</c>, <c>READY</c> or <c>ERRORED</c>.</param>
/// <param name="DurationSeconds">Duration in seconds, or <c>null</c> when unknown.</param>
/// <param name="AspectRatio">Aspect ratio (e.g. <c>16:9</c>), or <c>null</c> when unknown.</param>
public record ExerciseVideoResponse(
    string? PlaybackId,
    string Status,
    decimal? DurationSeconds,
    string? AspectRatio);
