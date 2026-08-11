namespace Mentora.Core.DTOs.ProgramTemplate.Body;

public class ProgramTemplateSession
{
    public string Name { get; set; } = null!;

    /// <summary>PRESENTIEL_SOLO | PRESENTIEL_GROUPE | VISIO | A_DISTANCE.</summary>
    public string Type { get; set; } = null!;

    /// <summary>1 (Monday) through 7 (Sunday).</summary>
    public int DayOfWeek { get; set; }

    public int Position { get; set; }

    public List<ProgramTemplateCircuit> Circuits { get; set; } = [];
}
