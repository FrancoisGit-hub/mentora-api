namespace Mentora.Core.Entities;

/// <summary>
/// Lot 12.2a — join row EXERCISE_SECONDARY_MUSCLE_GROUPS (composite PK: exercise + muscle group).
/// EXERCISES.EXERCISE_SECONDARY_MUSCLES (jsonb) stays as the display mirror of these links.
/// </summary>
public class ExerciseSecondaryMuscleGroup
{
    public Guid ExerciseSecondaryMuscleGroupExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public Guid ExerciseSecondaryMuscleGroupMuscleGroupId { get; set; }
    public MuscleGroupItem MuscleGroup { get; set; } = null!;
}
