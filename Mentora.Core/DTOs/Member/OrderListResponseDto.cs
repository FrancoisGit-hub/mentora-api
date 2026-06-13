namespace Mentora.Core.DTOs.Member;

/// <summary>
/// Paginated list of a member's orders, as returned by <c>GET /api/v1/member/orders</c>.
/// </summary>
/// <param name="Orders">Orders on the current page, sorted by creation date descending.</param>
/// <param name="NextCursor">
/// Opaque cursor to supply as the <c>cursor</c> query parameter to retrieve the next page,
/// or <c>null</c> when this is the last page.
/// </param>
public record OrderListResponseDto(
    IReadOnlyList<OrderSummaryDto> Orders,
    string? NextCursor);
