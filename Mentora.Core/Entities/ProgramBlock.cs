namespace Mentora.Core.Entities;

public class ProgramBlock
{
    public Guid ProgramBlockId { get; set; }

    public Guid ProgramBlockProgramId { get; set; }

    // Self-reference — NULL only for the root MACROCYCLE block
    public Guid? ProgramBlockParentId { get; set; }

    public string ProgramBlockLevel { get; set; } = null!;    // MACROCYCLE | MESOCYCLE | MICROCYCLE
    public string ProgramBlockName { get; set; } = null!;
    public int ProgramBlockPosition { get; set; }
    public int? ProgramBlockWeekNumber { get; set; }          // MICROCYCLE only
}
