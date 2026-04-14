using Mentora.Core.DTOs.Coach;

namespace Mentora.Core.Interfaces;

public interface ICoachParameterService
{
    Task<CoachParameterResponse> GetParametersAsync(Guid coachId);
    Task<CoachParameterResponse> UpdateParametersAsync(Guid coachId, UpdateCoachParameterRequest request);
}
