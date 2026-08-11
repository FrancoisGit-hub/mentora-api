namespace Mentora.Core.DTOs.ProgramTemplate.Body;

/// <summary>A node in the periodization tree: MACROCYCLE, MESOCYCLE, or MICROCYCLE.</summary>
public class ProgramTemplateBlock
{
    /// <summary>MACROCYCLE | MESOCYCLE | MICROCYCLE.</summary>
    public string Level { get; set; } = null!;

    public string Name { get; set; } = null!;
    public int Position { get; set; }

    /// <summary>Required if and only if <see cref="Level"/> is MICROCYCLE.</summary>
    public int? WeekNumber { get; set; }

    public List<ProgramTemplateBlock> Blocks { get; set; } = [];

    /// <summary>Only populated on MICROCYCLE blocks.</summary>
    public List<ProgramTemplateSession> Sessions { get; set; } = [];
}
