using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Returns the authenticated member's full profile and vouchers for the mobile home screen.</summary>
[ApiController]
[Route("api/v1/member/me")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Me")]
public class MemberMeController(IMemberMeService memberMeService) : ControllerBase
{
    /// <summary>Returns the authenticated member's profile and all their vouchers grouped by coach.</summary>
    /// <param name="consumedSinceDays">
    /// Number of days back from now to include CONSUMED vouchers (default 90).
    /// 0 = no CONSUMED vouchers returned.
    /// Positive N = only CONSUMED vouchers whose VOUCHER_UPDATED_DATE is within the last N days.
    /// AVAILABLE and RESERVED vouchers are always returned regardless of this value.
    /// Negative values return 400.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <c>data</c> object with two properties:
    /// <c>profile</c> (member identity and activation state) and
    /// <c>coaches</c> (array of coach groups, each containing a coach summary and their vouchers).
    /// </returns>
    /// <remarks>
    /// Coaches are ordered: primary coach first (MEMBER_COACHES.IS_PRIMARY = true),
    /// then remaining coaches by MEMBER_COACHES.STARTED_AT ascending.
    /// A coach appears in the array even when the member has zero vouchers for them.
    /// Within each coach group, vouchers are sorted by status priority
    /// (RESERVED → AVAILABLE → CONSUMED), then by VOUCHER_UPDATED_DATE descending.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMe(
        [FromQuery] int consumedSinceDays = 90,
        CancellationToken ct = default)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await memberMeService.GetAsync(memberId, consumedSinceDays, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
