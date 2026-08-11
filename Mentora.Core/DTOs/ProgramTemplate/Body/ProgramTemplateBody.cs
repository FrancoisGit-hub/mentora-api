namespace Mentora.Core.DTOs.ProgramTemplate.Body;

/// <summary>Root of the recursive block tree persisted as PROGRAM_TEMPLATE_BODY (jsonb).</summary>
public class ProgramTemplateBody
{
    /// <summary>Schema version of this body, set to 1. Reserved for future migration.</summary>
    public int BodyVersion { get; set; } = 1;

    public List<ProgramTemplateBlock> Blocks { get; set; } = [];
}
