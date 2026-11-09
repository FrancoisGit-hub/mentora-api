using Mentora.Core.DTOs.Exercise;

namespace Mentora.Core.DTOs.AdminExercise;

// Lot 12.2b — admin back-office: Mentora exercise catalog (EXERCISE_COACH_ID NULL only).

/// <summary>
/// Body of POST and PUT /api/v1/admin/exercises (full replacement on PUT). Strings are trimmed, an empty
/// string becomes null. Not in the request: sourceCode, isActive, isPolyarticular, videoUrl, imageUrl and
/// the deprecated muscleGroup / equipment.
/// </summary>
/// <param name="Name">Required, 1 to 120 characters once trimmed; unique among Mentora exercises (accents, case and spaces ignored).</param>
/// <param name="NameEn">Optional English name, up to 120 characters.</param>
/// <param name="Description">Optional, up to 2000 characters.</param>
/// <param name="Instructions">Optional, up to 8000 characters.</param>
/// <param name="CategoryId">Required, an active EXERCISE_CATEGORIES row (or the one the exercise already holds).</param>
/// <param name="TypeId">Optional exercise type.</param>
/// <param name="LateralityId">Optional laterality.</param>
/// <param name="MovementPatternId">Optional movement pattern.</param>
/// <param name="StartingPositionId">Optional starting position.</param>
/// <param name="GripTypeId">Optional grip type.</param>
/// <param name="PrimaryMuscleGroupId">Optional primary muscle group.</param>
/// <param name="SecondaryMuscleGroupIds">0 to 6 distinct muscle groups, never the primary one.</param>
/// <param name="PrimaryEquipmentId">Required (the front preselects "Sans matériel").</param>
/// <param name="SecondaryEquipmentId">Optional, different from the primary equipment, never "Sans matériel".</param>
/// <param name="Level">Optional minimum level: BEGINNER, INTERMEDIATE or ADVANCED.</param>
/// <param name="Breathing">Optional, up to 2000 characters.</param>
/// <param name="CommonMistakes">0 to 20 items, each 1 to 300 characters once trimmed.</param>
/// <param name="Precautions">Optional, up to 2000 characters.</param>
/// <param name="CoachTip">Optional, up to 2000 characters.</param>
/// <param name="SearchTags">Optional, up to 500 characters.</param>
/// <param name="ObjectiveIds">0 to 20 distinct objectives.</param>
public record AdminExerciseRequest(
    string? Name,
    string? NameEn,
    string? Description,
    string? Instructions,
    Guid? CategoryId,
    Guid? TypeId,
    Guid? LateralityId,
    Guid? MovementPatternId,
    Guid? StartingPositionId,
    Guid? GripTypeId,
    Guid? PrimaryMuscleGroupId,
    IReadOnlyList<Guid>? SecondaryMuscleGroupIds,
    Guid? PrimaryEquipmentId,
    Guid? SecondaryEquipmentId,
    string? Level,
    string? Breathing,
    IReadOnlyList<string>? CommonMistakes,
    string? Precautions,
    string? CoachTip,
    string? SearchTags,
    IReadOnlyList<Guid>? ObjectiveIds);

/// <summary>Body of PUT /api/v1/admin/exercises/{exerciseId}/activation.</summary>
/// <param name="IsActive">Required: true to activate, false to deactivate.</param>
public record AdminExerciseActivationRequest(bool? IsActive);

/// <summary>Row of a closed reference list.</summary>
public record AdminExerciseReferenceItem(Guid Id, string Code, string Label, int SortOrder, bool IsActive);

/// <summary>Muscle group of the catalog.</summary>
public record AdminMuscleGroupRef(Guid Id, string Name, int SortOrder);

/// <summary>Equipment of the catalog.</summary>
public record AdminEquipmentRef(Guid Id, string Name, string? Category);

/// <summary>Identifier and name of a catalog row.</summary>
public record AdminNamedRef(Guid Id, string Name);

/// <summary>Identifier, code and label of a reference list row.</summary>
public record AdminReferenceRef(Guid Id, string Code, string Label);

/// <summary>Everything the admin form needs to fill its drop-down lists.</summary>
public record AdminExerciseReferencesResponse(
    IReadOnlyList<AdminExerciseReferenceItem> Categories,
    IReadOnlyList<AdminExerciseReferenceItem> Types,
    IReadOnlyList<AdminExerciseReferenceItem> Lateralities,
    IReadOnlyList<AdminExerciseReferenceItem> MovementPatterns,
    IReadOnlyList<AdminExerciseReferenceItem> StartingPositions,
    IReadOnlyList<AdminExerciseReferenceItem> GripTypes,
    IReadOnlyList<AdminMuscleGroupRef> MuscleGroups,
    IReadOnlyList<AdminEquipmentRef> Equipment,
    IReadOnlyList<AdminNamedRef> Objectives,
    IReadOnlyList<string> Levels);

/// <summary>Row of the admin exercise list.</summary>
public record AdminExerciseListItem(
    Guid ExerciseId,
    string? SourceCode,
    string Name,
    string? Category,
    string? PrimaryMuscleGroup,
    string? PrimaryEquipment,
    string? Level,
    bool IsActive,
    bool HasVideo,
    bool HasLegacyValues,
    DateTime UpdatedDate);

/// <summary>Page of the admin exercise list.</summary>
public record AdminExerciseListResponse(IReadOnlyList<AdminExerciseListItem> Items, string? NextCursor);

/// <summary>Free-text value kept for a list whose foreign key is null (nothing mapped it); each key is null otherwise.</summary>
public record AdminExerciseLegacyValues(
    string? Category,
    string? Type,
    string? Laterality,
    string? MovementPattern,
    string? StartingPosition,
    string? GripType);

/// <summary>Where an exercise is used (any status, any owner).</summary>
public record AdminExerciseUsage(int Programs, int ProgramTemplates, int SessionTemplates);

/// <summary>Detail of a Mentora exercise for the admin back-office.</summary>
public record AdminExerciseResponse(
    Guid ExerciseId,
    string? SourceCode,
    string Name,
    string? NameEn,
    string? Description,
    string? Instructions,
    AdminReferenceRef? Category,
    AdminReferenceRef? Type,
    AdminReferenceRef? Laterality,
    AdminReferenceRef? MovementPattern,
    AdminReferenceRef? StartingPosition,
    AdminReferenceRef? GripType,
    AdminExerciseLegacyValues LegacyValues,
    bool IsPolyarticular,
    AdminNamedRef? PrimaryMuscleGroup,
    IReadOnlyList<AdminNamedRef> SecondaryMuscleGroups,
    AdminNamedRef? PrimaryEquipment,
    AdminNamedRef? SecondaryEquipment,
    string? Level,
    string? Breathing,
    IReadOnlyList<string> CommonMistakes,
    string? Precautions,
    string? CoachTip,
    string? SearchTags,
    IReadOnlyList<AdminNamedRef> Objectives,
    ExerciseVideoResponse? Video,
    bool IsActive,
    AdminExerciseUsage Usage,
    DateTime CreatedDate,
    DateTime UpdatedDate);
