using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Provides read-only access to the authenticated member's orders.</summary>
[ApiController]
[Route("api/v1/member/orders")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Orders")]
public class MemberOrdersController(IOrderService orderService) : ControllerBase
{
    /// <summary>Lists all orders for the authenticated member, newest first.</summary>
    /// <param name="status">Optional status filter (PENDING, PAID, EXPIRED, FAILED, CANCELLED).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of orders matching the filter, or all orders if no filter is provided.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] OrderStatus? status, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await orderService.ListForMemberAsync(memberId, status, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Returns a single order by ID, verifying it belongs to the authenticated member.</summary>
    /// <param name="orderId">Unique identifier of the order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full order details including line items.</returns>
    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid orderId, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await orderService.GetForMemberAsync(memberId, orderId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
