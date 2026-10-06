using Mentora.Core.DTOs.SessionTemplate;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>
/// Session template library of the authenticated coach: the coach's own templates plus the active Mentora
/// platform templates. Platform templates are read-only (they come from the seeder); a platform, another
/// coach's or an unknown template is a 404 on every route. Envelope { success, data, error, statusCode }.
/// </summary>
[ApiController]
[Route("api/v1/coach/session-templates")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Session templates")]
public class CoachSessionTemplatesController(ISessionTemplateService service) : ControllerBase
{
    /// <summary>
    /// Lists the visible templates (cursor pagination, ordered by name ignoring case and accents, then id).
    /// </summary>
    /// <param name="scope"><c>mine</c>, <c>platform</c> or <c>all</c> (default), case insensitive.</param>
    /// <param name="nature">Optional: STANDARD | EVALUATION.</param>
    /// <param name="sessionType">Optional: PRESENTIEL_SOLO | PRESENTIEL_GROUPE | VISIO | AUTONOME | PRESENTIEL_OU_VISIO.</param>
    /// <param name="search">Optional, 2 to 60 characters, on the name only, case and accent insensitive.</param>
    /// <param name="limit">Page size, 1 to 100 (default 50).</param>
    /// <param name="cursor">The <c>nextCursor</c> of the previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>data = { items, nextCursor }</c>. 400 on an invalid scope, filter, search length, limit or cursor.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? scope,
        [FromQuery] string? nature,
        [FromQuery] string? sessionType,
        [FromQuery] string? search,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await service.ListAsync(coachId, scope, nature, sessionType, search, limit, cursor, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Returns a full template: its fields, its circuits and <c>sessionNode</c>, a ready-to-insert program tree
    /// session node (no id, no sessionId; dayOfWeek and position are placeholders set by the app).
    /// </summary>
    /// <param name="id">Template id.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await service.GetByIdAsync(id, coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Creates a template owned by the authenticated coach. Circuits follow the program tree rules; every
    /// exercise must be visible to the coach (Mentora catalog or the coach's own).
    /// </summary>
    /// <param name="request">Name, description, nature, sessionType and circuits.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] SessionTemplateRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await service.CreateAsync(request, coachId, ct);
        return Created(
            $"/api/v1/coach/session-templates/{result.Id}",
            new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }

    /// <summary>
    /// Fully replaces the coach's own template. A platform template, another coach's or an unknown one is a 404.
    /// </summary>
    /// <param name="id">Template id.</param>
    /// <param name="request">The replacement data.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SessionTemplateRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await service.UpdateAsync(id, request, coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Physically deletes the coach's own template (nothing references a template). A platform template,
    /// another coach's or an unknown one is a 404.
    /// </summary>
    /// <param name="id">Template id.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        await service.DeleteAsync(id, coachId, ct);
        return NoContent();
    }

    /// <summary>
    /// Copies a visible template (platform or own) into the coach's library: new id, owned by the caller,
    /// no source code, name suffixed " (copie)" (kept within 100 characters). No body.
    /// </summary>
    /// <param name="id">Id of the template to copy.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/duplicate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await service.DuplicateAsync(id, coachId, ct);
        return Created(
            $"/api/v1/coach/session-templates/{result.Id}",
            new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }
}
