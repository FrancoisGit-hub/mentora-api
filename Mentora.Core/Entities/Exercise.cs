using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class Exercise
{
    public Guid ExerciseId { get; set; }

    // NULL means "Mentora catalogue entry"
    public Guid? ExerciseCoachId { get; set; }
    public Coach? Coach { get; set; }

    public string ExerciseName { get; set; } = null!;
    public string? ExerciseDescription { get; set; }
    public string? ExerciseInstructions { get; set; }
    public string? ExerciseVideoUrl { get; set; }
    public string? ExerciseImageUrl { get; set; }

    /// <summary>Deprecated since Lot 9: derived from the new reference tables, kept for the current coach app, to be dropped in Lot 10.</summary>
    public MuscleGroup ExerciseMuscleGroup { get; set; }

    /// <summary>Deprecated since Lot 9: derived from the new reference tables, kept for the current coach app, to be dropped in Lot 10.</summary>
    public Equipment ExerciseEquipment { get; set; }

    public bool ExerciseIsPolyarticular { get; set; } = false;
    public bool ExerciseIsActive { get; set; } = true;

    // Lot 9 catalog columns — all nullable, filled by later sub-lots.
    public string? ExerciseSourceCode { get; set; }
    public string? ExerciseNameEn { get; set; }
    public string? ExerciseCategory { get; set; }
    public string? ExerciseMovementPattern { get; set; }
    public string? ExerciseType { get; set; }
    public string? ExerciseLaterality { get; set; }
    public string? ExerciseStartingPosition { get; set; }
    public string? ExerciseGripType { get; set; }

    public Guid? ExercisePrimaryMuscleGroupId { get; set; }
    public MuscleGroupItem? PrimaryMuscleGroup { get; set; }
    public List<string>? ExerciseSecondaryMuscles { get; set; }

    public Guid? ExercisePrimaryEquipmentId { get; set; }
    public EquipmentItem? PrimaryEquipment { get; set; }
    public Guid? ExerciseSecondaryEquipmentId { get; set; }
    public EquipmentItem? SecondaryEquipment { get; set; }

    public ExerciseLevel? ExerciseMinLevel { get; set; }
    public string? ExerciseBreathing { get; set; }
    public List<string>? ExerciseCommonMistakes { get; set; }
    public string? ExercisePrecautions { get; set; }
    public string? ExerciseCoachTip { get; set; }
    public string? ExerciseSearchTags { get; set; }

    public ExerciseVideo? Video { get; set; }
    public List<ExerciseObjective> ExerciseObjectives { get; set; } = [];

    public DateTime ExerciseCreatedDate { get; set; }
    public DateTime ExerciseUpdatedDate { get; set; }
}
