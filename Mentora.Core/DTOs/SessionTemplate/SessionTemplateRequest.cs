using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.DTOs.SessionTemplate;

/// <summary>Body of POST (create) and PUT (full replace) of a coach session template.</summary>
public class SessionTemplateRequest
{
    /// <summary>1 to 100 characters once trimmed.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Up to 1000 characters once trimmed; empty becomes null.</summary>
    public string? Description { get; set; }

    /// <summary>STANDARD | EVALUATION.</summary>
    public string Nature { get; set; } = null!;

    /// <summary>PRESENTIEL_SOLO | PRESENTIEL_GROUPE | VISIO | AUTONOME | PRESENTIEL_OU_VISIO.</summary>
    public string SessionType { get; set; } = null!;

    /// <summary>At least one circuit, in the program tree circuit format (same rules as in a program).</summary>
    public List<ProgramTemplateCircuit> Circuits { get; set; } = [];
}
