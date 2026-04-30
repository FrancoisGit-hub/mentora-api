using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class SessionSlot
{
    public Guid SessionSlotId { get; set; }
    public DateTime SessionSlotStartDate { get; set; }
    public DateTime SessionSlotEndDate { get; set; }
    public OfferType SessionSlotOfferType { get; set; }
    public int SessionSlotDurationMinutes { get; set; }
    public bool SessionSlotIsAvailable { get; set; }
    public DateTime SessionSlotCreatedDate { get; set; }

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;
}
