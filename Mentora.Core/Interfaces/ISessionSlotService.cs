using Mentora.Core.DTOs.Coach;
using Mentora.Core.DTOs.Lot2;
using Mentora.Core.DTOs.Member;

namespace Mentora.Core.Interfaces;

public interface ISessionSlotService
{
    Task<SessionSlotDto> CreateAsync(Guid coachId, CreateSessionSlotRequest request);
    Task<SessionSlotDto> UpdateAsync(Guid coachId, Guid slotId, UpdateSessionSlotRequest request);
    Task DeleteAsync(Guid coachId, Guid slotId);

    /// <summary>
    /// Returns the coach's own session slots within [from, to] (inclusive), optionally filtered
    /// by availability. Booked-slot identity (session/member) is resolved in the same query.
    /// Validation (both dates required, to &gt;= from, range &lt;= 186 days) happens inside the service.
    /// </summary>
    Task<List<CoachSessionSlotDto>> ListForCoachAsync(
        Guid coachId, ListSessionSlotsRequest request, CancellationToken ct);

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
