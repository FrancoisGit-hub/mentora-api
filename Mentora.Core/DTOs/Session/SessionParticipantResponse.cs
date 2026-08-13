namespace Mentora.Core.DTOs.Session;

/// <param name="ParticipantId">Unique identifier of the registration row.</param>
/// <param name="SessionId">Identifier of the group session.</param>
/// <param name="MemberId">Identifier of the registered member.</param>
/// <param name="MemberFirstName">First name of the registered member.</param>
/// <param name="MemberLastName">Last name of the registered member.</param>
/// <param name="VoucherId">Identifier of the voucher consumed by this registration.</param>
/// <param name="Status">REGISTERED, ATTENDED, NO_SHOW, or CANCELLED. Cancelled rows are kept for history.</param>
/// <param name="CreatedDate">Date the registration was created (UTC).</param>
/// <param name="UpdatedDate">Date the registration was last updated (UTC).</param>
public record SessionParticipantResponse(
    Guid ParticipantId,
    Guid SessionId,
    Guid MemberId,
    string MemberFirstName,
    string MemberLastName,
    Guid? VoucherId,
    string Status,
    DateTime CreatedDate,
    DateTime UpdatedDate);
