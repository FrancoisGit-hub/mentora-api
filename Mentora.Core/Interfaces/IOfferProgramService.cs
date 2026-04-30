using Mentora.Core.DTOs.Lot2;

namespace Mentora.Core.Interfaces;

public interface IOfferProgramService
{
    Task<List<OfferProgramDto>> GetAllAsync(Guid coachId);
    Task<OfferProgramDto> CreateAsync(Guid coachId, CreateOfferProgramRequest request);
    Task<OfferProgramDto> UpdateAsync(Guid coachId, Guid programId, UpdateOfferProgramRequest request);
    Task DeleteAsync(Guid coachId, Guid programId);
}
