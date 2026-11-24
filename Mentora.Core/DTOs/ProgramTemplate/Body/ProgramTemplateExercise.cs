namespace Mentora.Core.DTOs.ProgramTemplate.Body;

public class ProgramTemplateExercise
{
    public Guid ExerciseId { get; set; }
    public int Position { get; set; }

    /// <summary>KG | BODYWEIGHT. ELASTIC is declared but rejected in V1.</summary>
    public string LoadType { get; set; } = null!;

    /// <summary>Required for SERIE_SIMPLE, BISET, TRISET and GEANTE; forbidden for INTERVAL; accepted for the other modes.</summary>
    public int? PrescribedSets { get; set; }
    public int? PrescribedReps { get; set; }

    public decimal? PrescribedWeightKg { get; set; }
    /// <summary>
    /// Rest after each set of this exercise (in an alternating circuit, rest after this
    /// exercise's set before the next one).
    /// </summary>
    public int? RestSecondsAfterSeries { get; set; }

    /// <summary>Required for INTERVAL; forbidden for SERIE_SIMPLE, BISET, TRISET and GEANTE; accepted for the other modes.</summary>
    public int? WorkSeconds { get; set; }
    public int? RestWorkSeconds { get; set; }

    public string? CustomNote { get; set; }
}
