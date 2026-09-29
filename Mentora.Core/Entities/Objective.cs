namespace Mentora.Core.Entities;

public class Objective
{
    public Guid ObjectiveId { get; set; }
    public string ObjectiveName { get; set; } = null!;

    public DateTime ObjectiveCreatedDate { get; set; }
    public DateTime ObjectiveUpdatedDate { get; set; }
}
