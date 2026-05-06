using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class SessionVoucher
{
    public Guid VoucherId { get; set; }

    public Guid OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;

    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    // Weak reference — no FK constraint; product may be archived after voucher creation
    public Guid ProductId { get; set; }

    // Snapshot fields
    public OfferType OfferType { get; set; }
    public int DurationMinutes { get; set; }
    public Sport Sport { get; set; } = Sport.Training;

    public VoucherStatus Status { get; set; } = VoucherStatus.Available;

    // FK to SESSIONS deferred to Lot 2.4 migration — nullable Guid, no constraint yet
    public Guid? ReservedSessionId { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}
