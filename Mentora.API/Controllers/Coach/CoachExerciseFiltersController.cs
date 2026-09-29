using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>Provides the filter values usable on the coach exercise list.</summary>
[ApiController]
[Route("api/v1/coach/exercise-filters")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Exercises")]
public class CoachExerciseFiltersController(IExerciseService exerciseService) : ControllerBase
{
    /// <summary>
    /// Returns the muscle groups, equipment, objectives and levels usable as filters. Each list
    /// only contains values used by at least one active exercise visible to the authenticated
    /// coach (Mentora catalogue plus the coach's own).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await exerciseService.GetFiltersForCoachAsync(coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
