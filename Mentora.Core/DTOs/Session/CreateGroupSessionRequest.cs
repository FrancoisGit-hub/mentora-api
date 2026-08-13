namespace Mentora.Core.DTOs.Session;

/// <param name="SlotId">
/// The slot to create the group session on. Must belong to the coach, be available, carry a
/// group SESSION_SLOT_OFFER_TYPE, and not already carry a session.
/// </param>
/// <param name="ProductId">
/// The product this group session is offered under. Must belong to the coach, be Published,
/// have a group OfferType, and match the slot's OfferType and DurationMinutes. Snapshotted onto
/// the session (Sport, ProductId, MaxParticipants) at creation — no lookup, no default.
/// </param>
public record CreateGroupSessionRequest(Guid SlotId, Guid ProductId);
