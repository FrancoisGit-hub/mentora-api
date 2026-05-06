using Mentora.Core.DTOs.Session;
using Mentora.Core.Enums;

namespace Mentora.Core.Interfaces;

public interface ISessionService
{
    // Member-side
    Task<SessionResponse> ReserveAsync(
        Guid memberId, ReserveSessionRequest request, CancellationToken ct);
    Task<SessionResponse> CancelByMemberAsync(
        Guid memberId, Guid sessionId, CancelSessionRequest request, CancellationToken ct);
    Task<IReadOnlyList<SessionResponse>> ListForMemberAsync(
        Guid memberId, SessionStatusFilter? statusFilter, CancellationToken ct);
    Task<SessionResponse> GetForMemberAsync(
        Guid memberId, Guid sessionId, CancellationToken ct);

    // Coach-side
    Task<SessionResponse> CancelByCoachAsync(
        Guid coachId, Guid sessionId, CancelSessionRequest request, CancellationToken ct);
    Task<IReadOnlyList<SessionResponse>> ListForCoachAsync(
        Guid coachId, SessionStatusFilter? statusFilter, DateTime? fromDate, DateTime? toDate, CancellationToken ct);
    Task<SessionResponse> GetForCoachAsync(
        Guid coachId, Guid sessionId, CancellationToken ct);
}
