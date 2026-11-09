using Mentora.API.Authorization;
using Mentora.Core.DTOs.AdminExercise;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Admin;

/// <summary>
/// Lot 12.2b — edition of the Mentora exercise catalog by the associates (AdminBearer token only).
/// Scope: Mentora exercises (no coach owner), inactive included; a coach-owned or unknown id is a 404.
/// Errors go through GlobalExceptionMiddleware (400 invalid parameter, 401, 404, 409, 422).
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
[ApiExplorerSettings(GroupName = AdminAuthDefaults.SwaggerDoc)]
[Tags("Admin — Exercises")]
public class AdminExercisesController(IAdminExerciseService exerciseService) : ControllerBase
{
    /// <summary>
    /// The reference lists of the exercise form: categories, types, lateralities, movement patterns,
    /// starting positions and grip types (by sort order), muscle groups, equipment ("Sans matériel" first),
    /// objectives (by name) and the level values.
    /// </summary>
    [HttpGet("exercise-references")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetReferences(CancellationToken ct)
    {
        var result = await exerciseService.GetReferencesAsync(ct);
        return Ok(Envelope(result, 200));
    }

    /// <summary>
    /// Lists the Mentora exercises, inactive included, by name then id with cursor pagination. Filters are
    /// combined with AND. With <c>search</c>: same rules and ranking as the member search; a term shaped like a
    /// source code (<c>EX029</c>, <c>lg28</c>) filters on that exact code instead.
    /// </summary>
    /// <param name="search">Optional, 2 to 60 characters.</param>
    /// <param name="isActive">true, false, or absent for both.</param>
    /// <param name="categoryId">Optional category.</param>
    /// <param name="muscleGroupId">Optional primary muscle group.</param>
    /// <param name="equipmentId">Optional equipment, primary or secondary.</param>
    /// <param name="objectiveId">Optional objective.</param>
    /// <param name="level">Optional exact level: BEGINNER, INTERMEDIATE or ADVANCED (not "accessible at").</param>
    /// <param name="hasLegacyValues">true: at least one list holds text with a null foreign key; false: none does.</param>
    /// <param name="limit">Page size, 1 to 100. Defaults to 50.</param>
    /// <param name="cursor">Opaque <c>nextCursor</c> of the previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("exercises")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? muscleGroupId,
        [FromQuery] Guid? equipmentId,
        [FromQuery] Guid? objectiveId,
        [FromQuery] string? level,
        [FromQuery] bool? hasLegacyValues,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        var result = await exerciseService.ListAsync(
            search, isActive, categoryId, muscleGroupId, equipmentId, objectiveId, level, hasLegacyValues,
            limit, cursor, ct);
        return Ok(Envelope(result, 200));
    }

    /// <summary>Detail of a Mentora exercise: reference rows, legacy values, usage counts and video.</summary>
    /// <param name="exerciseId">Exercise identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("exercises/{exerciseId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid exerciseId, CancellationToken ct)
    {
        var result = await exerciseService.GetByIdAsync(exerciseId, ct);
        return Ok(Envelope(result, 200));
    }

    /// <summary>
    /// Creates a Mentora exercise (active, source code allocated: next EX number). 409 EXERCISE_NAME_TAKEN
    /// when the name is already used (details: the existing exercise), 422 for an invalid or unknown reference.
    /// </summary>
    /// <param name="request">Exercise body.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("exercises")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] AdminExerciseRequest request, CancellationToken ct)
    {
        var result = await exerciseService.CreateAsync(request, GetAdminUserId(), ct);
        return StatusCode(StatusCodes.Status201Created, Envelope(result, 201));
    }

    /// <summary>
    /// Replaces every editable field. When no stored value changes the answer is 200 with no write and no
    /// audit row.
    /// </summary>
    /// <param name="exerciseId">Exercise identifier.</param>
    /// <param name="request">Exercise body.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("exercises/{exerciseId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid exerciseId, [FromBody] AdminExerciseRequest request, CancellationToken ct)
    {
        var result = await exerciseService.UpdateAsync(exerciseId, request, GetAdminUserId(), ct);
        return Ok(Envelope(result, 200));
    }

    /// <summary>
    /// Activates or deactivates an exercise. Same state: 200, nothing written. Deactivating an exercise used
    /// by a program, a program template or a session template: 409 EXERCISE_IN_USE (details: counts and the
    /// names of the Mentora templates).
    /// </summary>
    /// <param name="exerciseId">Exercise identifier.</param>
    /// <param name="request">Body with <c>isActive</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("exercises/{exerciseId:guid}/activation")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetActivation(
        Guid exerciseId, [FromBody] AdminExerciseActivationRequest request, CancellationToken ct)
    {
        var result = await exerciseService.SetActivationAsync(exerciseId, request, GetAdminUserId(), ct);
        return Ok(Envelope(result, 200));
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
