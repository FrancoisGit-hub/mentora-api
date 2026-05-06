using Mentora.Core.DTOs.Catalog;

namespace Mentora.Core.Interfaces;

public interface IProductPackService
{
    Task<List<ProductPackResponse>> GetByCoachAsync(Guid coachId);
    Task<ProductPackResponse> GetByIdAsync(Guid packId, Guid coachId);
    Task<ProductPackResponse> CreateAsync(ProductPackRequest request, Guid coachId);
    Task<ProductPackResponse> UpdateAsync(Guid packId, ProductPackRequest request, Guid coachId);
    Task DeleteAsync(Guid packId, Guid coachId);
    Task<ProductPackResponse> PublishAsync(Guid packId, Guid coachId, CancellationToken ct);
    Task<ProductPackResponse> UnpublishAsync(Guid packId, Guid coachId, CancellationToken ct);
}
