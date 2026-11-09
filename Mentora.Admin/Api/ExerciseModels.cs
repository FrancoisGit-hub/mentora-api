namespace Mentora.Admin.Api;

// DTO clients de /api/v1/admin/exercises et /api/v1/admin/exercise-references (JSON en camelCase).
// Aucune référence au projet API : ces records suivent le JSON lu au lot 12.2b.

/// <summary>Ligne d'une liste fermée (catégories, types, latéralités, schémas, positions, prises).</summary>
public sealed record RefItem(Guid Id, string Code, string Label, int SortOrder, bool IsActive);

public sealed record MuscleGroupRef(Guid Id, string Name, int SortOrder);

public sealed record EquipmentRef(Guid Id, string Name, string? Category);

public sealed record NamedRef(Guid Id, string Name);

public sealed record CodedRef(Guid Id, string Code, string Label);

public sealed record ExerciseReferences(
    IReadOnlyList<RefItem> Categories,
    IReadOnlyList<RefItem> Types,
    IReadOnlyList<RefItem> Lateralities,
    IReadOnlyList<RefItem> MovementPatterns,
    IReadOnlyList<RefItem> StartingPositions,
    IReadOnlyList<RefItem> GripTypes,
    IReadOnlyList<MuscleGroupRef> MuscleGroups,
    IReadOnlyList<EquipmentRef> Equipment,
    IReadOnlyList<NamedRef> Objectives,
    IReadOnlyList<string> Levels);

public sealed record ExerciseListItem(
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

public sealed record ExerciseListPage(IReadOnlyList<ExerciseListItem> Items, string? NextCursor);

public sealed record LegacyValues(
    string? Category,
    string? Type,
    string? Laterality,
    string? MovementPattern,
    string? StartingPosition,
    string? GripType);

public sealed record ExerciseUsage(int Programs, int ProgramTemplates, int SessionTemplates);

public sealed record ExerciseVideo(string? PlaybackId, string Status, decimal? DurationSeconds, string? AspectRatio);

public sealed record ExerciseDetail(
    Guid ExerciseId,
    string? SourceCode,
    string Name,
    string? NameEn,
    string? Description,
    string? Instructions,
    CodedRef? Category,
    CodedRef? Type,
    CodedRef? Laterality,
    CodedRef? MovementPattern,
    CodedRef? StartingPosition,
    CodedRef? GripType,
    LegacyValues LegacyValues,
    bool IsPolyarticular,
    NamedRef? PrimaryMuscleGroup,
    IReadOnlyList<NamedRef> SecondaryMuscleGroups,
    NamedRef? PrimaryEquipment,
    NamedRef? SecondaryEquipment,
    string? Level,
    string? Breathing,
    IReadOnlyList<string> CommonMistakes,
    string? Precautions,
    string? CoachTip,
    string? SearchTags,
    IReadOnlyList<NamedRef> Objectives,
    ExerciseVideo? Video,
    bool IsActive,
    ExerciseUsage Usage,
    DateTime CreatedDate,
    DateTime UpdatedDate);

/// <summary>Corps de POST et PUT (remplacement complet) : chaque champ est toujours envoyé.</summary>
public sealed record ExerciseRequest(
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
    IReadOnlyList<Guid> SecondaryMuscleGroupIds,
    Guid? PrimaryEquipmentId,
    Guid? SecondaryEquipmentId,
    string? Level,
    string? Breathing,
    IReadOnlyList<string> CommonMistakes,
    string? Precautions,
    string? CoachTip,
    string? SearchTags,
    IReadOnlyList<Guid> ObjectiveIds);

public sealed record ActivationBody(bool IsActive);

/// <summary>Filtres de la liste. Search n'est envoyé qu'à partir de 2 caractères.</summary>
public sealed record ExerciseFilter(
    string? Search,
    Guid? CategoryId,
    Guid? MuscleGroupId,
    string? Level,
    bool? IsActive,
    bool? HasLegacyValues);
