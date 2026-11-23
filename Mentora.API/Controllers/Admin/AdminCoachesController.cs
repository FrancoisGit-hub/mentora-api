using Mentora.API.Authorization;
using Mentora.Core.DTOs.AdminCoach;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Admin;

/// <summary>
/// Lot 14.8a: the Mentora team lists and creates coach accounts (AdminBearer token only). No modification,
/// deactivation or deletion route. Errors go through GlobalExceptionMiddleware (400 invalid parameter, 401, 409, 422).
/// </summary>
[ApiController]
[Route("api/v1/admin/coaches")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
[ApiExplorerSettings(GroupName = AdminAuthDefaults.SwaggerDoc)]
[Tags("Admin: Coaches")]
public class AdminCoachesController(IAdminCoachService coachService) : ControllerBase
{
    /// <summary>
    /// Lists every coach (disabled users and coach-and-member accounts included), newest first, with cursor
    /// pagination. <c>data</c> is <c>{ items, nextCursor }</c>.
    /// </summary>
    /// <param name="limit">Page size, 1 to 100. Defaults to 50.</param>
    /// <param name="cursor">Opaque <c>nextCursor</c> of the previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        var result = await coachService.ListAsync(limit, cursor, ct);
        return Ok(Envelope(result, 200));
    }

    /// <summary>
    /// Creates a coach account (active, default parameters and offer programs) and sends the welcome mail after
    /// the commit, best effort (the answer does not say whether it went out). 409 EMAIL_TAKEN when any account
    /// already uses the email (details: existingAccount COACH, MEMBER, BOTH or NONE, and isEnabled).
    /// </summary>
    /// <param name="request">Coach body.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] AdminCreateCoachRequest request, CancellationToken ct)
    {
        var result = await coachService.CreateAsync(request, GetAdminUserId(), ct);
        return StatusCode(StatusCodes.Status201Created, Envelope(result, 201));
    }

    /// <summary>The admin user id of the AdminBearer token; a missing or malformed claim is a 401, never a default.</summary>
    private Guid GetAdminUserId()
    {
        if (!Guid.TryParse(User.FindFirst(AdminAuthDefaults.AdminIdClaim)?.Value, out var adminUserId))
            throw new UnauthorizedException("Admin account not found or disabled.");
        return adminUserId;
    }

    private static object Envelope(object data, int statusCode) =>
        new { success = true, data, error = (string?)null, statusCode };
}
