using Mentora.Core.DTOs.Exercise;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>Manages the exercise catalogue available to the authenticated coach.</summary>
[ApiController]
[Route("api/v1/coach/exercises")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Exercises")]
public class CoachExercisesController(IExerciseService exerciseService) : ControllerBase
{
    /// <summary>
    /// Lists exercises visible to the authenticated coach: shared Mentora catalogue entries
    /// plus the coach's own.
    /// </summary>
    /// <param name="search">Optional search (case and accent insensitive). Matches the exercise name and its alternative names (English name, search aliases) only, never descriptions or instructions. Results are ranked by relevance: name starts with the term, then a word of the name starts with it, then the name contains it, then alternative names only; ties by name.</param>
    /// <param name="muscleGroup">Deprecated since Lot 9, use <c>muscleGroupId</c>. Removed in Lot 10. Legacy muscle group filter (wire value, e.g. <c>CHEST</c>).</param>
    /// <param name="equipment">Deprecated since Lot 9, use <c>equipmentId</c>. Removed in Lot 10. Legacy equipment filter (wire value, e.g. <c>BARBELL</c>).</param>
    /// <param name="muscleGroupId">Optional primary muscle group filter.</param>
    /// <param name="equipmentId">Optional equipment filter, matching primary or secondary equipment.</param>
    /// <param name="objectiveId">Optional objective filter.</param>
    /// <param name="level">
    /// Optional level (<c>BEGINNER</c>, <c>INTERMEDIATE</c>, <c>ADVANCED</c>): returns exercises whose minimum
    /// level is lower than or equal to it. Exercises with an unknown level are excluded when given.
    /// </param>
    /// <param name="scope">
    /// <c>ALL</c> (default) returns Mentora entries and this coach's own; <c>MENTORA</c> restricts
    /// to shared catalogue entries; <c>MINE</c> restricts to this coach's own entries.
    /// </param>
    /// <param name="includeInactive">When <c>true</c>, includes soft-deleted (inactive) exercises. Defaults to <c>false</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? muscleGroup,
        [FromQuery] string? equipment,
        [FromQuery] Guid? muscleGroupId,
        [FromQuery] Guid? equipmentId,
        [FromQuery] Guid? objectiveId,
        [FromQuery] string? level,
        [FromQuery] string scope = "ALL",
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await exerciseService.ListForCoachAsync(
            coachId, search, muscleGroup, equipment, muscleGroupId, equipmentId, objectiveId, level,
            scope, includeInactive, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Returns a single exercise by ID: either a shared Mentora entry or one owned by the
    /// authenticated coach.
    /// </summary>
    /// <param name="exerciseId">Unique identifier of the exercise.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{exerciseId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid exerciseId, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await exerciseService.GetByIdForCoachAsync(exerciseId, coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Creates a new exercise owned by the authenticated coach.</summary>
    /// <param name="request">The exercise data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created exercise. The Location header points to the resource URL.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] ExerciseRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await exerciseService.CreateAsync(request, coachId, ct);
        return Created(
            $"/api/v1/coach/exercises/{result.ExerciseId}",
            new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }

    /// <summary>
    /// Fully replaces all fields of an existing exercise. The exercise must be owned by the
    /// authenticated coach — a shared Mentora entry or another coach's exercise 404s.
    /// </summary>
    /// <param name="exerciseId">Unique identifier of the exercise to update.</param>
    /// <param name="request">The replacement exercise data.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{exerciseId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid exerciseId, [FromBody] ExerciseRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await exerciseService.UpdateAsync(exerciseId, request, coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Soft-deletes (deactivates) an exercise owned by the authenticated coach — a shared
    /// Mentora entry or another coach's exercise 404s.
    /// </summary>
    /// <param name="exerciseId">Unique identifier of the exercise to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{exerciseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid exerciseId, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        await exerciseService.DeleteAsync(exerciseId, coachId, ct);
        return NoContent();
    }
}
