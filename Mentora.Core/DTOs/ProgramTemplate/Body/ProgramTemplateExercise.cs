namespace Mentora.Core.DTOs.ProgramTemplate.Body;

public class ProgramTemplateExercise
{
    public Guid ExerciseId { get; set; }
    public int Position { get; set; }

    /// <summary>KG | BODYWEIGHT. ELASTIC is declared but rejected in V1.</summary>
    public string LoadType { get; set; } = null!;

    /// <summary>STANDARD circuits only.</summary>
    public int? PrescribedSets { get; set; }
    public int? PrescribedReps { get; set; }

    public decimal? PrescribedWeightKg { get; set; }
    public int? RestSeconds { get; set; }

    /// <summary>INTERVAL circuits only.</summary>
    public int? WorkSeconds { get; set; }
    public int? RestWorkSeconds { get; set; }

    public string? CustomNote { get; set; }
}
