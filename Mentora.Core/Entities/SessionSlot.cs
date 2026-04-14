namespace Mentora.Core.Entities;

public class SessionSlot
{
    public Guid SessionSlotId { get; set; }
    public DateTime SessionSlotStartDate { get; set; }
    public DateTime SessionSlotEndDate { get; set; }
    public decimal SessionSlotPriceEuros { get; set; }
    public int SessionSlotCreditsRequired { get; set; }
    public string SessionSlotType { get; set; } = null!;
    public bool SessionSlotIsAvailable { get; set; }
    public DateTime SessionSlotCreatedDate { get; set; }

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    public Session? Session { get; set; }
}
