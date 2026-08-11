using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Provides read-only access to the exercise catalogue visible to the authenticated member.</summary>
[ApiController]
[Route("api/v1/member/exercises")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Exercises")]
public class MemberExercisesController(IExerciseService exerciseService) : ControllerBase
{
    /// <summary>
    /// Lists active exercises visible to the authenticated member: shared Mentora catalogue
    /// entries plus those owned by any coach the member is linked to.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await exerciseService.ListForMemberAsync(memberId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Returns a single active exercise by ID, visible to the authenticated member.</summary>
    /// <param name="exerciseId">Unique identifier of the exercise.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{exerciseId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid exerciseId, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await exerciseService.GetByIdForMemberAsync(exerciseId, memberId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
