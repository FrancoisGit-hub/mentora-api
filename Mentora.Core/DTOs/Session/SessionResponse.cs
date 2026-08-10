namespace Mentora.Core.DTOs.Session;

/// <param name="SessionId">Unique identifier of the session.</param>
/// <param name="VoucherId">Identifier of the voucher this session was booked from.</param>
/// <param name="SlotId">Identifier of the slot this session was booked on.</param>
/// <param name="CoachId">Identifier of the coach.</param>
/// <param name="CoachDisplayName">The coach's first and last name.</param>
/// <param name="MemberId">Identifier of the member the session is booked for.</param>
/// <param name="MemberFirstName">First name of the member.</param>
/// <param name="MemberLastName">Last name of the member.</param>
/// <param name="ProductId">Identifier of the product this session was booked from (weak reference — product may be archived).</param>
/// <param name="ProductName">Product name, or "(unavailable)" if the product no longer resolves.</param>
/// <param name="OfferType">Session format: VISIO or PRESENTIEL_SOLO.</param>
/// <param name="DurationMinutes">Session duration in minutes.</param>
/// <param name="Sport">Sport discipline.</param>
/// <param name="ScheduledAt">Date and time the session is scheduled for (UTC).</param>
/// <param name="Status">Effective status: SCHEDULED, COMPLETED, or CANCELLED.</param>
/// <param name="VisioUrl">Video call URL, present only for non-cancelled VISIO sessions.</param>
/// <param name="EffectiveAddress">
/// Resolved at read time, never snapshotted: this member's per-coach address override
/// (MEMBER_COACHES.MEMBER_COACH_PRESENTIAL_ADDRESS) if set, else the booked product's own
/// location. Populated for in-person (non-VISIO) sessions only; null for VISIO sessions.
/// </param>
/// <param name="CancellationReason">Reason given for cancellation, if cancelled.</param>
/// <param name="CancelledBy">Who cancelled the session: MEMBER or COACH, if cancelled.</param>
/// <param name="CancelledAt">Date the session was cancelled (UTC), if cancelled.</param>
/// <param name="CreatedDate">Date the session was created (UTC).</param>
/// <param name="UpdatedDate">Date the session was last updated (UTC).</param>
public record SessionResponse(
    Guid SessionId,
    Guid VoucherId,
    Guid SlotId,
    Guid CoachId,
    string CoachDisplayName,
    Guid MemberId,
    string MemberFirstName,
    string MemberLastName,
    Guid ProductId,
    string ProductName,
    string OfferType,
    int DurationMinutes,
    string Sport,
    DateTime ScheduledAt,
    string Status,
    string? VisioUrl,
    string? EffectiveAddress,
    string? CancellationReason,
    string? CancelledBy,
    DateTime? CancelledAt,
    DateTime CreatedDate,
    DateTime UpdatedDate);
