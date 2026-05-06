using Mentora.Core.DTOs.Order;
using Mentora.Core.Enums;

namespace Mentora.Core.Interfaces;

public interface IOrderService
{
    Task<IReadOnlyList<OrderResponse>> ListForMemberAsync(
        Guid memberId, OrderStatus? statusFilter, CancellationToken ct);

    Task<OrderResponse> GetForMemberAsync(
        Guid memberId, Guid orderId, CancellationToken ct);
}
