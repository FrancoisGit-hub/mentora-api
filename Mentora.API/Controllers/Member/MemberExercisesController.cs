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
    /// Lists active exercises visible to the authenticated member (light shape), sorted by name
    /// then id, with cursor pagination. Visible: shared Mentora catalogue entries plus those
    /// owned by any coach the member is attached to. Filters are combined with AND.
    /// </summary>
    /// <param name="muscleGroupId">Optional primary muscle group filter.</param>
    /// <param name="equipmentId">Optional equipment filter, matching primary or secondary equipment.</param>
    /// <param name="objectiveId">Optional objective filter.</param>
    /// <param name="level">
    /// Optional level (<c>BEGINNER</c>, <c>INTERMEDIATE</c>, <c>ADVANCED</c>): returns exercises whose minimum
    /// level is lower than or equal to it. Exercises with an unknown level are excluded when given.
    /// </param>
    /// <param name="search">Optional case-insensitive substring on name, English name and search tags (2 to 60 characters).</param>
    /// <param name="limit">Page size, from 1 to 100. Defaults to 50.</param>
    /// <param name="cursor">Opaque cursor returned as <c>nextCursor</c> by the previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? muscleGroupId,
        [FromQuery] Guid? equipmentId,
        [FromQuery] Guid? objectiveId,
        [FromQuery] string? level,
        [FromQuery] string? search,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await exerciseService.ListForMemberAsync(
            memberId, muscleGroupId, equipmentId, objectiveId, level, search, limit, cursor, ct);
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
