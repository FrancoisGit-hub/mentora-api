using Mentora.Core.DTOs.Voucher;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>Lets the authenticated coach grant free session vouchers to one of their members.</summary>
[ApiController]
[Route("api/v1/coach/members/{memberId:guid}/voucher-grants")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Vouchers")]
public class CoachVoucherGrantsController(IVoucherGrantService voucherGrantService) : ControllerBase
{
    /// <summary>
    /// Grants free session vouchers on one of the coach's own products to a member currently
    /// attached to the coach. Records a zero-amount, already-completed purchase (origin
    /// <c>COACH_GRANT</c>) and generates vouchers with the same logic as a paid member purchase.
    /// Product publication is not required — a coach may grant sessions on a private (draft) offer.
    /// </summary>
    /// <param name="memberId">Unique identifier of the member to grant vouchers to.</param>
    /// <param name="request">The grant payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Grant(
        Guid memberId, [FromBody] VoucherGrantRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await voucherGrantService.GrantAsync(coachId, memberId, request, ct);
        return StatusCode(201, new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }
}
