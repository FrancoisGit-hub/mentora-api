using Mentora.Core.DTOs.Sessions;

namespace Mentora.Core.Interfaces;

public interface ISessionService
{
    Task<BookSessionResponse> BookSessionAsync(Guid userId, Guid coachId, Guid slotId);
    Task<CancelSessionResponse> CancelSessionAsync(Guid userId, Guid coachId, Guid sessionId, string? reason);
    Task<List<SessionResponse>> GetSessionsAsync(Guid userId, Guid coachId, string? status);
    Task<SessionDetailResponse> GetSessionByIdAsync(Guid userId, Guid coachId, Guid sessionId);
}
