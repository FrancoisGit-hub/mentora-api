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

    /// <summary>
    /// Load coefficient for the block. Allowed only when <see cref="Level"/> is MESOCYCLE;
    /// null, 100, 102.5, 105 or 110. Stored and returned as is, never computed by the API.
    /// </summary>
    public decimal? LoadPercent { get; set; }

    public List<ProgramTemplateBlock> Blocks { get; set; } = [];

    /// <summary>Only populated on MICROCYCLE blocks.</summary>
    public List<ProgramTemplateSession> Sessions { get; set; } = [];
}
