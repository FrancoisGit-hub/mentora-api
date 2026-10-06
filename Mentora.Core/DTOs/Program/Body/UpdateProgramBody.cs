using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.DTOs.Program.Body;

/// <summary>
/// Tree of <c>PUT /api/v1/coach/training-programs/{programId}</c>. Same shape and property names
/// as <see cref="ProgramTemplateBody"/>, plus an optional <c>Id</c> on every node: a node with an
/// id updates that existing row in place (history kept), a node without id is inserted.
/// </summary>
public class UpdateProgramBody
{
    /// <summary>Schema version of this body, set to 1. Reserved for future migration.</summary>
    public int BodyVersion { get; set; } = 1;

    public List<UpdateProgramBlock> Blocks { get; set; } = [];

    /// <summary>Copy without ids, validated by the shared ProgramTemplateBodyValidator.</summary>
    public ProgramTemplateBody ToTemplateBody() => new()
    {
        BodyVersion = BodyVersion,
        Blocks      = Blocks?.Select(b => b?.ToTemplateBlock()!).ToList() ?? [],
    };
}
