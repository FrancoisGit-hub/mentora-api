using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.DTOs.Program.Body;

public class UpdateProgramCircuit
{
    /// <summary>Existing PROGRAM_CIRCUIT_ID of this program to update in place; null inserts a new circuit.</summary>
    public Guid? Id { get; set; }

    public string Name { get; set; } = null!;
    public int Position { get; set; }

    /// <summary>STANDARD | INTERVAL.</summary>
    public string Mode { get; set; } = null!;

    /// <summary>Required when <see cref="Mode"/> is INTERVAL.</summary>
    public int? Rounds { get; set; }

    public int? RestBetweenRoundsSeconds { get; set; }

    /// <summary>Rest after the whole circuit, before the next circuit. 0 to 600, STANDARD and INTERVAL.</summary>
    public int? RestSecondsAfterExercices { get; set; }
    public string? Note { get; set; }

    public List<UpdateProgramExercise> Exercises { get; set; } = [];

    public ProgramTemplateCircuit ToTemplateCircuit() => new()
    {
        Name                     = Name,
        Position                 = Position,
        Mode                     = Mode,
        Rounds                   = Rounds,
        RestBetweenRoundsSeconds = RestBetweenRoundsSeconds,
        RestSecondsAfterExercices         = RestSecondsAfterExercices,
        Note                     = Note,
        Exercises                = Exercises?.Select(e => e?.ToTemplateExercise()!).ToList() ?? [],
    };
}
