namespace Mentora.Core.Entities;

public class ProgramCircuit
{
    public Guid ProgramCircuitId { get; set; }

    // Denormalized on purpose — see WHY PROGRAM_ID IS DENORMALIZED ON ALL FIVE (ProgramConfiguration.cs)
    public Guid ProgramCircuitProgramId { get; set; }
    public Guid ProgramCircuitProgramSessionId { get; set; }

    public string ProgramCircuitName { get; set; } = null!;
    public int ProgramCircuitPosition { get; set; }
    public string ProgramCircuitMode { get; set; } = null!;   // one of the 12 modes of Mentora.Core.Programs.CircuitModes
    public int? ProgramCircuitRounds { get; set; }
    public int? ProgramCircuitRestBetweenRoundsSeconds { get; set; }
    public int? ProgramCircuitRestAfterSeconds { get; set; }
    public string? ProgramCircuitNote { get; set; }
}
