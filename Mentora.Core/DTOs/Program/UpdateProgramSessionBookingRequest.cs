namespace Mentora.Core.DTOs.Program;

/// <param name="SessionId">
/// The booking to link this program session to. Null detaches — clears the link without
/// touching the booking itself (no voucher/participant/session changes).
/// </param>
public record UpdateProgramSessionBookingRequest(Guid? SessionId);
