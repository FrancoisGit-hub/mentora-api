using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Provides read-only access to the authenticated member's session vouchers.</summary>
[ApiController]
[Route("api/v1/member/vouchers")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Vouchers")]
public class MemberVouchersController(IVoucherService voucherService) : ControllerBase
{
    /// <summary>
    /// Lists vouchers for the authenticated member, grouped by coach, newest first within each group.
    /// </summary>
    /// <remarks>
    /// When <paramref name="status"/> is absent the endpoint returns only vouchers with status
    /// <c>AVAILABLE</c>. Supply one of the accepted values to filter to a different status.
    ///
    /// Accepted values for <paramref name="status"/>: <c>AVAILABLE</c>, <c>RESERVED</c>, <c>CONSUMED</c>.
    /// Any other value (including lowercase variants or integers) results in a 400 response.
    ///
    /// The response is an array of coach groups. Each group contains the matching vouchers for that
    /// coach. Groups are ordered by the member's coach relationship start date (oldest first).
    /// A coach group is omitted entirely when no vouchers match the filter for that coach.
    /// </remarks>
    /// <param name="status">
    /// Optional status filter. Defaults to <c>AVAILABLE</c> when absent.
    /// Accepted: <c>AVAILABLE</c> | <c>RESERVED</c> | <c>CONSUMED</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An array of coach groups, each containing the matching vouchers.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await voucherService.ListForMemberAsync(memberId, status, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Returns a single voucher by ID, verifying it belongs to the authenticated member.</summary>
    /// <param name="voucherId">Unique identifier of the voucher.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The voucher details.</returns>
    [HttpGet("{voucherId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid voucherId, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await voucherService.GetForMemberAsync(memberId, voucherId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
