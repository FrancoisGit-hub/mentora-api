using Mentora.Core.DTOs.Visio;

namespace Mentora.Core.Interfaces;

public interface IVisioTokenService
{
    Task<VisioTokenResponse> IssueForCoachAsync(Guid coachId, Guid sessionId, CancellationToken ct);
    Task<VisioTokenResponse> IssueForMemberAsync(Guid memberId, Guid sessionId, CancellationToken ct);
}
