using Mentora.Core.DTOs.ProgramTemplate;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>Manages the program template library available to the authenticated coach.</summary>
[ApiController]
[Route("api/v1/coach/program-templates")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Program templates")]
public class CoachProgramTemplatesController(IProgramTemplateService programTemplateService) : ControllerBase
{
    /// <summary>
    /// Lists program template headers visible to the authenticated coach: shared Mentora
    /// templates plus the coach's own. Never returns the body — payload size matters.
    /// </summary>
    /// <param name="search">Optional case-insensitive substring filter on the template name.</param>
    /// <param name="goal">Optional goal filter (wire value, e.g. <c>MUSCLE_GAIN</c>).</param>
    /// <param name="scope">
    /// <c>ALL</c> (default) returns Mentora templates and this coach's own; <c>MENTORA</c>
    /// restricts to shared templates; <c>MINE</c> restricts to this coach's own.
    /// </param>
    /// <param name="includeInactive">When <c>true</c>, includes soft-deleted (inactive) templates. Defaults to <c>false</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? goal,
        [FromQuery] string scope = "ALL",
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await programTemplateService.ListForCoachAsync(coachId, search, goal, scope, includeInactive, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Returns a single program template by ID, header and full body: either a shared Mentora
    /// template or one owned by the authenticated coach.
    /// </summary>
    /// <param name="programTemplateId">Unique identifier of the program template.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{programTemplateId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid programTemplateId, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await programTemplateService.GetByIdForCoachAsync(programTemplateId, coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Creates a new program template owned by the authenticated coach.</summary>
    /// <param name="request">The program template data, including its block tree body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created program template. The Location header points to the resource URL.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] ProgramTemplateRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await programTemplateService.CreateAsync(request, coachId, ct);
        return Created(
            $"/api/v1/coach/program-templates/{result.ProgramTemplateId}",
            new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }

    /// <summary>
    /// Fully replaces all fields of an existing program template. The template must be owned by
    /// the authenticated coach — a shared Mentora template or another coach's template 404s.
    /// </summary>
    /// <param name="programTemplateId">Unique identifier of the program template to update.</param>
    /// <param name="request">The replacement program template data.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{programTemplateId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid programTemplateId, [FromBody] ProgramTemplateRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await programTemplateService.UpdateAsync(programTemplateId, request, coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Soft-deletes (deactivates) a program template owned by the authenticated coach — a shared
    /// Mentora template or another coach's template 404s.
    /// </summary>
    /// <param name="programTemplateId">Unique identifier of the program template to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{programTemplateId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid programTemplateId, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        await programTemplateService.DeleteAsync(programTemplateId, coachId, ct);
        return NoContent();
    }
}
