using Mentora.Core.DTOs.Catalog;

namespace Mentora.Core.Interfaces;

public interface IProductService
{
    Task<List<ProductResponse>> GetByCoachAsync(Guid coachId);
    Task<ProductResponse> GetByIdAsync(Guid productId, Guid coachId);
    Task<ProductResponse> CreateAsync(ProductRequest request, Guid coachId);
    Task<ProductResponse> UpdateAsync(Guid productId, ProductRequest request, Guid coachId);
    Task DeleteAsync(Guid productId, Guid coachId);
    Task<ProductResponse> PublishAsync(Guid productId, Guid coachId, CancellationToken ct);
    Task<ProductResponse> UnpublishAsync(Guid productId, Guid coachId, CancellationToken ct);
}
