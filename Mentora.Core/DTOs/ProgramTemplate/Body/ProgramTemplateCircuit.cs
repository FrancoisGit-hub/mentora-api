namespace Mentora.Core.DTOs.ProgramTemplate.Body;

/// <remarks>
/// Execution rule: SERIE_SIMPLE circuit with one exercise = classic sets (all sets of the exercise);
/// BISET, TRISET and GEANTE = alternating rounds (superset / triset / giant set): one set of each
/// exercise in order, then repeat. RestSecondsAfterExercices = rest after the whole circuit, before the
/// next circuit.
/// </remarks>
public class ProgramTemplateCircuit
{
    public string Name { get; set; } = null!;
    public int Position { get; set; }

    /// <summary>
    /// SERIE_SIMPLE | BISET | TRISET | GEANTE | CIRCUIT | AMRAP | EMOM | FOR_TIME | INTERVAL | TEMPS_CONTINU |
    /// DISTANCE | LIBRE (uppercase, exact). STANDARD is a deprecated alias of SERIE_SIMPLE: still accepted
    /// on write, always stored and returned as SERIE_SIMPLE.
    /// </summary>
    public string Mode { get; set; } = null!;

    /// <summary>Required when <see cref="Mode"/> is INTERVAL.</summary>
    public int? Rounds { get; set; }

    public int? RestBetweenRoundsSeconds { get; set; }

    /// <summary>Rest after the whole circuit, before the next circuit. 0 to 600, every mode.</summary>
    public int? RestSecondsAfterExercices { get; set; }
    public string? Note { get; set; }

    public List<ProgramTemplateExercise> Exercises { get; set; } = [];
}
