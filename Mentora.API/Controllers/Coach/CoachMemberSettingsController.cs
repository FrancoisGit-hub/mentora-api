using Mentora.Core.DTOs.Coach;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>A member's settings as managed by their coach — currently just the presential-address override.</summary>
[ApiController]
[Route("api/v1/coach/members/{memberId:guid}/parameters")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Member Settings")]
public class CoachMemberSettingsController(ICoachMemberSettingsService service) : ControllerBase
{
    /// <summary>
    /// Returns the specified member's settings. 404 (not 403) if the member doesn't exist or
    /// isn't linked to the authenticated coach — the two cases must be indistinguishable.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromRoute] Guid memberId, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await service.GetAsync(coachId, memberId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Replaces the coach's presential-address override for this member. 404 (not 403) if the
    /// member doesn't exist or isn't linked to the authenticated coach.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update(
        [FromRoute] Guid memberId, [FromBody] UpdateMemberSettingsRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await service.UpdateAsync(coachId, memberId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
