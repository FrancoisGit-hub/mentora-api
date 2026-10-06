using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.DTOs.Program.Body;

public class UpdateProgramSession
{
    /// <summary>
    /// Existing PROGRAM_SESSION_ID of this program to update in place; null inserts a new session.
    /// The booking link, status and realised values of an existing session are kept.
    /// </summary>
    public Guid? Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>PRESENTIEL_SOLO | PRESENTIEL_GROUPE | VISIO | AUTONOME | PRESENTIEL_OU_VISIO.</summary>
    public string Type { get; set; } = null!;

    /// <summary>STANDARD | EVALUATION. Null or absent means STANDARD.</summary>
    public string? Nature { get; set; }

    /// <summary>1 (Monday) through 7 (Sunday).</summary>
    public int DayOfWeek { get; set; }

    public int Position { get; set; }

    public List<UpdateProgramCircuit> Circuits { get; set; } = [];

    public ProgramTemplateSession ToTemplateSession() => new()
    {
        Name      = Name,
        Type      = Type,
        Nature    = Nature,
        DayOfWeek = DayOfWeek,
        Position  = Position,
        Circuits  = Circuits?.Select(c => c?.ToTemplateCircuit()!).ToList() ?? [],
    };
}
