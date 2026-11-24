using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.DTOs.Program.Body;

public class UpdateProgramExercise
{
    /// <summary>
    /// Existing PROGRAM_EXERCISE_ID of this program to update in place; null inserts a new exercise.
    /// Prescribed values are replaced, realised (actual) values are never touched by this endpoint.
    /// </summary>
    public Guid? Id { get; set; }

    public Guid ExerciseId { get; set; }
    public int Position { get; set; }

    /// <summary>KG | BODYWEIGHT. ELASTIC is declared but rejected in V1.</summary>
    public string LoadType { get; set; } = null!;

    /// <summary>Required for SERIE_SIMPLE, BISET, TRISET and GEANTE; forbidden for INTERVAL; accepted for the other modes.</summary>
    public int? PrescribedSets { get; set; }
    public int? PrescribedReps { get; set; }

    public decimal? PrescribedWeightKg { get; set; }

    /// <summary>Rest after each set of this exercise.</summary>
    public int? RestSecondsAfterSeries { get; set; }

    /// <summary>Required for INTERVAL; forbidden for SERIE_SIMPLE, BISET, TRISET and GEANTE; accepted for the other modes.</summary>
    public int? WorkSeconds { get; set; }
    public int? RestWorkSeconds { get; set; }

    public string? CustomNote { get; set; }

    public ProgramTemplateExercise ToTemplateExercise() => new()
    {
        ExerciseId         = ExerciseId,
        Position           = Position,
        LoadType           = LoadType,
        PrescribedSets     = PrescribedSets,
        PrescribedReps     = PrescribedReps,
        PrescribedWeightKg = PrescribedWeightKg,
        RestSecondsAfterSeries        = RestSecondsAfterSeries,
        WorkSeconds        = WorkSeconds,
        RestWorkSeconds    = RestWorkSeconds,
        CustomNote         = CustomNote,
    };
}
