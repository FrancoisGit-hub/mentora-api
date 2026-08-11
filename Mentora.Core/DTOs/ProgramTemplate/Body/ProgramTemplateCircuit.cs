namespace Mentora.Core.DTOs.ProgramTemplate.Body;

public class ProgramTemplateCircuit
{
    public string Name { get; set; } = null!;
    public int Position { get; set; }

    /// <summary>STANDARD | INTERVAL.</summary>
    public string Mode { get; set; } = null!;

    /// <summary>Required when <see cref="Mode"/> is INTERVAL.</summary>
    public int? Rounds { get; set; }

    public int? RestBetweenRoundsSeconds { get; set; }
    public string? Note { get; set; }

    public List<ProgramTemplateExercise> Exercises { get; set; } = [];
}
