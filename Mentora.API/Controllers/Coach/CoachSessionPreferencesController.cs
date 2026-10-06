using Mentora.Core.DTOs.Coach;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>Which session modes the coach offers and their in-person location labels. Information only: nothing is blocked by these preferences.</summary>
[ApiController]
[Route("api/v1/coach/settings/session-preferences")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Session Preferences")]
public class CoachSessionPreferencesController(ICoachSessionPreferencesService service) : ControllerBase
{
    /// <summary>Returns the coach's session preferences. Responses: 200, 401, 403. Envelope { success, data, error, statusCode }.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await service.GetAsync(coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Full replace: offersVisio, offersInPersonSolo and inPersonLocations are all required.
    /// Locations: 0 to 10, each 1 to 100 characters once trimmed, no duplicates (case and accents ignored),
    /// empty when offersInPersonSolo is false. Responses: 200, 401, 403, 422.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSessionPreferencesRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await service.UpdateAsync(coachId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
