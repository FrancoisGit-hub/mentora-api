using Mentora.Core.DTOs.Lot2;

namespace Mentora.Core.Interfaces;

public interface ISessionSlotService
{
    Task<SessionSlotDto> CreateAsync(Guid coachId, CreateSessionSlotRequest request);
    Task<SessionSlotDto> UpdateAsync(Guid coachId, Guid slotId, UpdateSessionSlotRequest request);
    Task DeleteAsync(Guid coachId, Guid slotId);
    Task<List<SessionSlotDto>> GetAvailableSlotsForMemberAsync(Guid memberId, DateTime from, DateTime to);
}
