namespace Mentora.Core.DTOs.Exercise;

/// <summary>
/// Values usable as exercise filters, restricted to those used by at least one exercise
/// visible to the caller.
/// </summary>
/// <param name="MuscleGroups">Primary muscle groups, sorted by sort order.</param>
/// <param name="Equipment">Equipment (primary or secondary), "Sans matériel" first then by name.</param>
/// <param name="Objectives">Objectives, sorted by name.</param>
/// <param name="Levels">Accepted values of the <c>level</c> filter, from easiest to hardest.</param>
public record ExerciseFiltersResponse(
    IReadOnlyList<ExerciseFilterMuscleGroup> MuscleGroups,
    IReadOnlyList<ExerciseFilterEquipment> Equipment,
    IReadOnlyList<ExerciseObjectiveRef> Objectives,
    IReadOnlyList<string> Levels);

/// <summary>Muscle group usable as a filter.</summary>
/// <param name="MuscleGroupId">Muscle group identifier.</param>
/// <param name="Name">Muscle group name.</param>
/// <param name="SortOrder">Display order.</param>
public record ExerciseFilterMuscleGroup(Guid MuscleGroupId, string Name, int SortOrder);

/// <summary>Equipment usable as a filter.</summary>
/// <param name="EquipmentId">Equipment identifier.</param>
/// <param name="Name">Equipment name.</param>
/// <param name="Category">Equipment category, or <c>null</c>.</param>
public record ExerciseFilterEquipment(Guid EquipmentId, string Name, string? Category);
