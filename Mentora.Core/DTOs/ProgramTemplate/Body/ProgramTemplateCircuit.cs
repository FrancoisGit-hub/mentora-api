namespace Mentora.Core.DTOs.ProgramTemplate.Body;

/// <remarks>
/// Execution rule: STANDARD circuit with one exercise = classic sets (all sets of the exercise);
/// with several exercises = alternating rounds (superset / triset / giant set): one set of each
/// exercise in order, then repeat. RestAfterSeconds = rest after the whole circuit, before the
/// next circuit.
/// </remarks>
public class ProgramTemplateCircuit
{
    public string Name { get; set; } = null!;
    public int Position { get; set; }

    /// <summary>STANDARD | INTERVAL.</summary>
    public string Mode { get; set; } = null!;

    /// <summary>Required when <see cref="Mode"/> is INTERVAL.</summary>
    public int? Rounds { get; set; }

    public int? RestBetweenRoundsSeconds { get; set; }

    /// <summary>Rest after the whole circuit, before the next circuit. 0 to 600, STANDARD and INTERVAL.</summary>
    public int? RestAfterSeconds { get; set; }
    public string? Note { get; set; }

    public List<ProgramTemplateExercise> Exercises { get; set; } = [];
}
