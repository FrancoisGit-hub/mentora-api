using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class Session
{
    public Guid SessionId { get; set; }

    public Guid SessionVoucherId { get; set; }
    public SessionVoucher Voucher { get; set; } = null!;

    public Guid SessionSlotId { get; set; }
    public SessionSlot Slot { get; set; } = null!;

    public Guid SessionMemberId { get; set; }
    public Member Member { get; set; } = null!;

    public Guid SessionCoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    // Weak reference — no FK constraint; snapshot semantics (same as ORDER_ITEMS)
    public Guid SessionProductId { get; set; }

    // Snapshots — never recomputed from voucher/product/slot
    public OfferType SessionOfferType { get; set; }
    public int SessionDurationMinutes { get; set; }
    public Sport SessionSport { get; set; }
    public DateTime SessionScheduledAt { get; set; }

    public SessionStatus SessionStatus { get; set; } = SessionStatus.Scheduled;

    public string? SessionVisioUrl { get; set; }
    public string? SessionCancellationReason { get; set; }
    public CancelledBy? SessionCancelledBy { get; set; }
    public DateTime? SessionCancelledAt { get; set; }
    public DateTime? SessionCompletedAt { get; set; }

    public DateTime SessionCreatedDate { get; set; }
    public DateTime SessionUpdatedDate { get; set; }
}
