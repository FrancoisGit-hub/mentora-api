using Mentora.Core.DTOs.Cart;

namespace Mentora.Core.Interfaces;

public interface ICartService
{
    Task<IReadOnlyList<CartResponse>> ListAsync(Guid memberId, CancellationToken ct);
    Task<CartResponse> GetForCoachAsync(Guid memberId, Guid coachId, CancellationToken ct);
    Task<CartResponse> AddItemAsync(Guid memberId, Guid coachId, AddCartItemRequest request, CancellationToken ct);
    Task<CartResponse> UpdateItemQuantityAsync(Guid memberId, Guid coachId, Guid cartItemId, UpdateCartItemQuantityRequest request, CancellationToken ct);
    Task<CartResponse> RemoveItemAsync(Guid memberId, Guid coachId, Guid cartItemId, CancellationToken ct);
    Task<CartResponse> ClearAsync(Guid memberId, Guid coachId, CancellationToken ct);
    Task<CheckoutResponse> CheckoutAsync(Guid memberId, Guid coachId, CancellationToken ct);
}
