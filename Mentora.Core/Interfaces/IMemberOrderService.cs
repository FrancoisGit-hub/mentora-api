using Mentora.Core.DTOs.Member;
using Mentora.Core.Exceptions;

namespace Mentora.Core.Interfaces;

public interface IMemberOrderService
{
    /// <summary>
    /// Returns a paginated list of orders for the member, newest first.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown (→ 400) when <paramref name="statusFilter"/> is not a recognised wire value,
    /// or when <paramref name="limit"/> is outside [1..100].
    /// </exception>
    Task<OrderListResponseDto> ListAsync(
        Guid memberId, string? statusFilter, int limit, string? cursor, CancellationToken ct);

    /// <summary>
    /// Returns the full detail of a single order, including items and vouchers.
    /// </summary>
    /// <exception cref="NotFoundException">
    /// Thrown (→ 404) when the order does not exist, or when it exists but belongs to a different member.
    /// The two cases are intentionally indistinguishable to prevent information leakage.
    /// </exception>
    Task<OrderDetailDto> GetDetailAsync(
        Guid memberId, Guid orderId, CancellationToken ct);
}
