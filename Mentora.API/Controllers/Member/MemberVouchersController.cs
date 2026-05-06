using Mentora.Core.Enums;
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
    /// <summary>Lists all vouchers for the authenticated member, newest first.</summary>
    /// <param name="status">Optional status filter (AVAILABLE, RESERVED, USED, EXPIRED, CANCELLED).</param>
    /// <param name="coachId">Optional coach filter — returns only vouchers for this coach.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of vouchers matching the filters, or all vouchers if no filters are provided.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] VoucherStatus? status,
        [FromQuery] Guid? coachId,
        CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await voucherService.ListForMemberAsync(memberId, status, coachId, ct);
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
