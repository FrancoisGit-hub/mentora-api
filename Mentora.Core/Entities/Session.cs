namespace Mentora.Core.Entities;

public class Session
{
    public Guid SessionId { get; set; }
    public string SessionStatus { get; set; } = null!;
    public int SessionCreditsConsumed { get; set; }
    public string? SessionCancelReason { get; set; }
    public DateTime? SessionCancelledDate { get; set; }
    public DateTime SessionCreatedDate { get; set; }

    public Guid SessionSlotId { get; set; }
    public SessionSlot SessionSlot { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;
}
