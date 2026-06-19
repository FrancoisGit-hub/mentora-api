using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Provides read-only access to available session slots for the authenticated member.</summary>
[ApiController]
[Route("api/v1/member/session-slots")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Session Slots")]
public class MemberSessionSlotsController(ISessionSlotService sessionSlotService) : ControllerBase
{
    /// <summary>
    /// Returns available session slots for the specified coach, optionally filtered by voucher compatibility.
    /// Each slot includes matching product info for the mobile "Buy this slot" CTA.
    /// </summary>
    /// <remarks>
    /// <para><b>Required:</b> <paramref name="coachId"/> must be provided; omitting it returns 400.</para>
    ///
    /// <para><b>Date range defaults:</b> when <paramref name="fromDate"/> is absent it defaults to
    /// <c>NOW()</c> (UTC); when <paramref name="toDate"/> is absent it defaults to <c>NOW() + 60 days</c>.
    /// The window is capped at 90 days; wider ranges return 400.</para>
    ///
    /// <para><b>Voucher compatibility (strict mode):</b> when <paramref name="voucherId"/> is supplied,
    /// the endpoint validates the voucher (ownership, coach match, AVAILABLE status — 404 or 400 on failure)
    /// and returns only slots whose offer type and duration exactly match the voucher.
    /// Each slot in the response includes <c>compatibleWithVoucherId: true</c>.
    /// When <paramref name="voucherId"/> is absent, <c>compatibleWithVoucherId</c> is omitted entirely.</para>
    ///
    /// <para><b>Product CTA fields (<c>productId</c>, <c>productName</c>, <c>productPriceEuros</c>):</b>
    /// each slot carries the first PUBLISHED product of the coach (by <c>PRODUCT_CREATED_DATE ASC</c>) whose
    /// <c>(offerType, durationMinutes)</c> matches the slot. All three fields are <c>null</c> when no
    /// published product matches — the mobile app should hide the CTA in that case.</para>
    ///
    /// <para><b>Validation error cases (400):</b> missing coachId; fromDate ≥ toDate; range &gt; 90 days;
    /// voucher not AVAILABLE; voucher coach does not match <paramref name="coachId"/>.</para>
    /// <para><b>Not-found case (404):</b> voucherId not found, or found but belonging to another member
    /// (indistinguishable — no information leakage).</para>
    /// </remarks>
    /// <param name="coachId">
    /// <b>Required.</b> UUID of the coach whose slots to list.
    /// </param>
    /// <param name="voucherId">
    /// Optional UUID of an AVAILABLE voucher owned by the member. When provided, only slots compatible
    /// with this voucher (same offer type and duration) are returned.
    /// </param>
    /// <param name="fromDate">
    /// Optional inclusive lower bound on slot start date (UTC). Defaults to <c>NOW()</c>.
    /// </param>
    /// <param name="toDate">
    /// Optional exclusive upper bound on slot start date (UTC). Defaults to <c>NOW() + 60 days</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of available session slots, sorted by start date ascending.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailable(
        [FromQuery] Guid?     coachId   = null,
        [FromQuery] Guid?     voucherId = null,
        [FromQuery] DateTime? fromDate  = null,
        [FromQuery] DateTime? toDate    = null,
        CancellationToken ct = default)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await sessionSlotService.GetAvailableSlotsForMemberAsync(
            memberId, coachId, voucherId, fromDate, toDate, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
