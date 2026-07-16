using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>Returns the authenticated coach's full home-screen payload.</summary>
[ApiController]
[Route("api/v1/coach/me")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Me")]
public class CoachMeController(ICoachMeService coachMeService) : ControllerBase
{
    /// <summary>Returns the coach's profile, active members, upcoming sessions, published offers, and stats.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <c>data</c> object with five properties: <c>profile</c>, <c>activeMembers</c>,
    /// <c>upcomingSessions</c> (next 7 days), <c>offers</c> (published products), and <c>stats</c>.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await coachMeService.GetAsync(coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
