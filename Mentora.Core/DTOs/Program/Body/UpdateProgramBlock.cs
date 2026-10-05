using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.DTOs.Program.Body;

/// <summary>A node in the periodization tree: MACROCYCLE, MESOCYCLE, or MICROCYCLE.</summary>
public class UpdateProgramBlock
{
    /// <summary>Existing PROGRAM_BLOCK_ID of this program to update in place; null inserts a new block.</summary>
    public Guid? Id { get; set; }

    /// <summary>MACROCYCLE | MESOCYCLE | MICROCYCLE.</summary>
    public string Level { get; set; } = null!;

    public string Name { get; set; } = null!;
    public int Position { get; set; }

    /// <summary>Required if and only if <see cref="Level"/> is MICROCYCLE.</summary>
    public int? WeekNumber { get; set; }

    /// <summary>Allowed only when <see cref="Level"/> is MESOCYCLE; null, 100, 102.5, 105 or 110.</summary>
    public decimal? LoadPercent { get; set; }

    public List<UpdateProgramBlock> Blocks { get; set; } = [];

    /// <summary>Only populated on MICROCYCLE blocks.</summary>
    public List<UpdateProgramSession> Sessions { get; set; } = [];

    public ProgramTemplateBlock ToTemplateBlock() => new()
    {
        Level       = Level,
        Name        = Name,
        Position    = Position,
        WeekNumber  = WeekNumber,
        LoadPercent = LoadPercent,
        Blocks      = Blocks?.Select(b => b.ToTemplateBlock()).ToList() ?? [],
        Sessions    = Sessions?.Select(s => s.ToTemplateSession()).ToList() ?? [],
    };
}
