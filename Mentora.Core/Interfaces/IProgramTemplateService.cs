using Mentora.Core.DTOs.ProgramTemplate;

namespace Mentora.Core.Interfaces;

public interface IProgramTemplateService
{
    Task<List<ProgramTemplateHeaderResponse>> ListForCoachAsync(
        Guid coachId, string? search, string? goal, string scope, bool includeInactive, CancellationToken ct);

    Task<ProgramTemplateResponse> GetByIdForCoachAsync(Guid programTemplateId, Guid coachId, CancellationToken ct);

    Task<ProgramTemplateResponse> CreateAsync(ProgramTemplateRequest request, Guid coachId, CancellationToken ct);

    Task<ProgramTemplateResponse> UpdateAsync(
        Guid programTemplateId, ProgramTemplateRequest request, Guid coachId, CancellationToken ct);

    Task DeleteAsync(Guid programTemplateId, Guid coachId, CancellationToken ct);
}
