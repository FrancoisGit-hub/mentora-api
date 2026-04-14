using Mentora.Core.DTOs.Sessions;

namespace Mentora.Core.Interfaces;

public interface ISessionSlotService
{
    Task<SessionSlotResponse> CreateSlotAsync(Guid coachId, CreateSessionSlotRequest request);
    Task<SessionSlotResponse> UpdateSlotAsync(Guid coachId, Guid slotId, CreateSessionSlotRequest request);
    Task DeleteSlotAsync(Guid coachId, Guid slotId);
    Task<List<SessionSlotResponse>> GetAvailableSlotsAsync(Guid coachId, DateTime from, DateTime to);
}
