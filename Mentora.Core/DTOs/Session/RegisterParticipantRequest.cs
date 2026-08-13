namespace Mentora.Core.DTOs.Session;

/// <param name="MemberId">The member to register. Must be linked to the coach, else 404.</param>
/// <param name="VoucherId">
/// The voucher to consume. Must belong to the member, be Available, and match the session's
/// OfferType and DurationMinutes, else 409.
/// </param>
public record RegisterParticipantRequest(Guid MemberId, Guid VoucherId);
