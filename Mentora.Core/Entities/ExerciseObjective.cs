namespace Mentora.Core.Entities;

/// <summary>Join row EXERCISE_OBJECTIVES (composite PK: exercise + objective).</summary>
public class ExerciseObjective
{
    public Guid ExerciseObjectiveExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public Guid ExerciseObjectiveObjectiveId { get; set; }
    public Objective Objective { get; set; } = null!;
}
