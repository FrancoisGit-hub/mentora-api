using Mentora.Core.DTOs.Program;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>Manages programs assigned by the authenticated coach to their members.</summary>
[ApiController]
[Route("api/v1/coach")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Programs")]
public class CoachProgramsController(IProgramService programService) : ControllerBase
{
    /// <summary>Lists program headers (no tree) for a member linked to the authenticated coach.</summary>
    /// <param name="memberId">Unique identifier of the member.</param>
    /// <param name="includeArchived">When <c>true</c>, includes archived programs. Defaults to <c>false</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("members/{memberId:guid}/programs")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAllForMember(
        Guid memberId, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await programService.ListForCoachAsync(coachId, memberId, includeArchived, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Returns a single program's full tree, owned by the authenticated coach.</summary>
    /// <param name="programId">Unique identifier of the program.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("programs/{programId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid programId, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await programService.GetByIdForCoachAsync(coachId, programId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Assigns a program to a member the authenticated coach is linked to — either a deep copy
    /// of a program template, or a manually supplied tree. Archives the member's current active
    /// program, if any.
    /// </summary>
    /// <param name="memberId">Unique identifier of the member.</param>
    /// <param name="request">The assignment payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created program and the id of the program it replaced, if any.</returns>
    [HttpPost("members/{memberId:guid}/programs")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Assign(Guid memberId, [FromBody] AssignProgramRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await programService.AssignAsync(coachId, memberId, request, ct);
        return Created(
            $"/api/v1/coach/programs/{result.Program.ProgramId}",
            new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }

    /// <summary>
    /// Fully replaces name, goal, startDate and the whole tree of an existing program owned by
    /// the authenticated coach. Never partial — deletes and reinserts the tree in one transaction.
    /// </summary>
    /// <param name="programId">Unique identifier of the program to update.</param>
    /// <param name="request">The replacement program data.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("programs/{programId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid programId, [FromBody] UpdateProgramRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await programService.UpdateAsync(coachId, programId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Archives a program owned by the authenticated coach. Never a row delete.</summary>
    /// <param name="programId">Unique identifier of the program to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("programs/{programId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid programId, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        await programService.DeleteAsync(coachId, programId, ct);
        return NoContent();
    }
}
