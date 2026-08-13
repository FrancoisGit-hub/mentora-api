using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class SessionParticipant
{
    public Guid SessionParticipantId { get; set; }

    public Guid SessionParticipantSessionId { get; set; }
    public Session Session { get; set; } = null!;

    public Guid SessionParticipantMemberId { get; set; }
    public Member Member { get; set; } = null!;

    // The voucher consumed by this registration
    public Guid? SessionParticipantVoucherId { get; set; }
    public SessionVoucher? Voucher { get; set; }

    public SessionParticipantStatus SessionParticipantStatus { get; set; } = SessionParticipantStatus.Registered;

    public DateTime SessionParticipantCreatedDate { get; set; }
    public DateTime SessionParticipantUpdatedDate { get; set; }
}
