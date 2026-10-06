using Mentora.Core.DTOs.Coach;

namespace Mentora.Core.Interfaces;

public interface ICoachSessionPreferencesService
{
    Task<SessionPreferencesDto> GetAsync(Guid coachId, CancellationToken ct);
    Task<SessionPreferencesDto> UpdateAsync(Guid coachId, UpdateSessionPreferencesRequest request, CancellationToken ct);
}
