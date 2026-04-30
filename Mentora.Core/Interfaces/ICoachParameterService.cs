using Mentora.Core.DTOs.Lot2;

namespace Mentora.Core.Interfaces;

public interface ICoachParameterService
{
    Task<CoachParameterDto> GetAsync(Guid coachId);
    Task<CoachParameterDto> UpdateAsync(Guid coachId, UpdateCoachParameterRequest request);
}
