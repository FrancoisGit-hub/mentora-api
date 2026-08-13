using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class Session
{
    public Guid SessionId { get; set; }

    // Null for group sessions — there is no single voucher, participants each consume their own
    // (SESSION_PARTICIPANTS.SESSION_PARTICIPANT_VOUCHER_ID). CK_SESSIONS_GROUP_HAS_NO_MEMBER
    // enforces this alongside SessionMemberId.
    public Guid? SessionVoucherId { get; set; }
    public SessionVoucher? Voucher { get; set; }

    public Guid SessionSlotId { get; set; }
    public SessionSlot Slot { get; set; } = null!;

    // Null for group sessions — membership lives in SESSION_PARTICIPANTS instead. See
    // CK_SESSIONS_GROUP_HAS_NO_MEMBER and V_SESSION_MEMBERS.
    public Guid? SessionMemberId { get; set; }
    public Member? Member { get; set; }

    public Guid SessionCoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    // Weak reference — no FK constraint; snapshot semantics (same as ORDER_ITEMS). Nullable at the
    // schema level (NOT NULL → nullable is always a safe direction), but in practice always set:
    // individual sessions snapshot it from the voucher, group sessions from the explicit productId
    // POST /coach/sessions/group requires. Deliberately left out of CK_SESSIONS_GROUP_HAS_NO_MEMBER
    // — unlike member/voucher, a group session's product is never null, so a "must be null for
    // groups" rule would be actively wrong.
    public Guid? SessionProductId { get; set; }

    // Snapshots — never recomputed from voucher/product/slot
    public OfferType SessionOfferType { get; set; }
    public int SessionDurationMinutes { get; set; }
    public Sport SessionSport { get; set; }
    public DateTime SessionScheduledAt { get; set; }

    // Group sessions only — snapshotted from PRODUCT_MAX_PARTICIPANTS at group-session creation.
    // NULL means no limit. Null for individual sessions.
    public int? SessionMaxParticipants { get; set; }

    public SessionStatus SessionStatus { get; set; } = SessionStatus.Scheduled;

    public string? SessionVisioUrl { get; set; }
    public string? SessionCancellationReason { get; set; }
    public CancelledBy? SessionCancelledBy { get; set; }
    public DateTime? SessionCancelledAt { get; set; }
    public DateTime? SessionCompletedAt { get; set; }

    public DateTime SessionCreatedDate { get; set; }
    public DateTime SessionUpdatedDate { get; set; }
}
