namespace Mentora.Core.Entities;

public class ProgramExercise
{
    public Guid ProgramExerciseId { get; set; }

    // Denormalized on purpose — see WHY PROGRAM_ID IS DENORMALIZED ON ALL FIVE (ProgramConfiguration.cs)
    public Guid ProgramExerciseProgramId { get; set; }
    public Guid ProgramExerciseCircuitId { get; set; }

    // Pointer, never copied — resolved separately, in one query, at read time
    public Guid ProgramExerciseExerciseId { get; set; }

    public int ProgramExercisePosition { get; set; }
    public string ProgramExerciseLoadType { get; set; } = null!;  // KG | BODYWEIGHT | ELASTIC
    public string? ProgramExerciseCustomNote { get; set; }

    public int? ProgramExercisePrescribedSets { get; set; }
    public int? ProgramExercisePrescribedReps { get; set; }
    public decimal? ProgramExercisePrescribedWeightKg { get; set; }
    public int? ProgramExerciseRestSeconds { get; set; }
    public int? ProgramExerciseWorkSeconds { get; set; }
    public int? ProgramExerciseRestWorkSeconds { get; set; }

    // Stay unwritten until Lot 6.6 — the prescribed values must never be overwritten by actuals.
    public int? ProgramExerciseActualSets { get; set; }
    public int? ProgramExerciseActualReps { get; set; }
    public decimal? ProgramExerciseActualWeightKg { get; set; }
    public int? ProgramExerciseActualRpe { get; set; }
    public string? ProgramExerciseMemberFeedback { get; set; }
}
