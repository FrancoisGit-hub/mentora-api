using Mentora.Core.DTOs.Lot2;
using Mentora.Core.DTOs.Member;

namespace Mentora.Core.Interfaces;

public interface ISessionSlotService
{
    Task<SessionSlotDto> CreateAsync(Guid coachId, CreateSessionSlotRequest request);
    Task<SessionSlotDto> UpdateAsync(Guid coachId, Guid slotId, UpdateSessionSlotRequest request);
    Task DeleteAsync(Guid coachId, Guid slotId);

    /// <summary>
    /// Returns available session slots for the given coach, filtered by date range and optionally
    /// by voucher compatibility. All validation (coachId presence, date range, voucher ownership
    /// and status) is performed inside the service.
    /// </summary>
    Task<List<MemberSessionSlotDto>> GetAvailableSlotsForMemberAsync(
        Guid memberId,
        Guid? coachId,
        Guid? voucherId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct);
}
