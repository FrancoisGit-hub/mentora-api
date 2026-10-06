using Mentora.Core.DTOs.SessionTemplate;

namespace Mentora.Core.Interfaces;

public interface ISessionTemplateService
{
    Task<SessionTemplateListResponse> ListAsync(
        Guid coachId, string? scope, string? nature, string? sessionType, string? search,
        int limit, string? cursor, CancellationToken ct);

    Task<SessionTemplateResponse> GetByIdAsync(Guid id, Guid coachId, CancellationToken ct);

    Task<SessionTemplateResponse> CreateAsync(SessionTemplateRequest request, Guid coachId, CancellationToken ct);

    Task<SessionTemplateResponse> UpdateAsync(Guid id, SessionTemplateRequest request, Guid coachId, CancellationToken ct);

    Task DeleteAsync(Guid id, Guid coachId, CancellationToken ct);

    Task<SessionTemplateResponse> DuplicateAsync(Guid id, Guid coachId, CancellationToken ct);
}
