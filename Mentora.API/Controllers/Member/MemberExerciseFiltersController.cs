using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Provides the filter values usable on the member exercise list.</summary>
[ApiController]
[Route("api/v1/member/exercise-filters")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Exercises")]
public class MemberExerciseFiltersController(IExerciseService exerciseService) : ControllerBase
{
    /// <summary>
    /// Returns the muscle groups, equipment, objectives and levels usable as filters. Each list
    /// only contains values used by at least one exercise visible to the authenticated member.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await exerciseService.GetFiltersForMemberAsync(memberId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
