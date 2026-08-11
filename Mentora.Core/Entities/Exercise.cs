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

    public MuscleGroup ExerciseMuscleGroup { get; set; }
    public Equipment ExerciseEquipment { get; set; }

    public bool ExerciseIsPolyarticular { get; set; } = false;
    public bool ExerciseIsActive { get; set; } = true;

    public DateTime ExerciseCreatedDate { get; set; }
    public DateTime ExerciseUpdatedDate { get; set; }
}
