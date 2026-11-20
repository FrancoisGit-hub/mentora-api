using Mentora.API.Authorization;
using Mentora.Core.DTOs.Coach;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mentora.API.Controllers.Coach;

/// <summary>Lot 14.1: lets the authenticated coach add a member by email.</summary>
[ApiController]
[Route("api/v1/coach/members")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach - Members")]
public class CoachMembersController(ICoachMemberInviteService inviteService) : ControllerBase
{
    /// <summary>
    /// Attaches a member to the calling coach by email. An unknown email creates the member account
    /// (the member then logs in with the usual email code); a member of another coach is only linked.
    /// Conflicts carry details.reason: ACCOUNT_DISABLED, EMAIL_IS_COACH, ALREADY_ATTACHED.
    /// A mail tells the member they were added (best effort, never changes the 201).
    /// </summary>
    /// <param name="request">Email, first name and last name of the member.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [EnableRateLimiting(MemberInviteRateLimiting.PolicyName)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Add([FromBody] AddMemberRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await inviteService.AddMemberAsync(coachId, request, ct);
        return StatusCode(201, new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }
}
