using Mentora.Core.DTOs.Lot2;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

[ApiController]
[Route("api/v1/coach/session-slots")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Session Slots")]
public class SessionSlotsController(ISessionSlotService sessionSlotService) : ControllerBase
{
    /// <summary>
    /// Lists the authenticated coach's own session slots within a date range, ordered by start
    /// date ascending. Booked slots carry the session and member identity; free slots carry null
    /// for all four.
    /// </summary>
    /// <param name="from">Inclusive lower bound on slot start date. Required.</param>
    /// <param name="to">Inclusive upper bound on slot start date. Required. Range capped at 186 days.</param>
    /// <param name="isAvailable">Optional filter on slot availability.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] bool? isAvailable,
        CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await sessionSlotService.ListForCoachAsync(
            coachId, new ListSessionSlotsRequest(from, to, isAvailable), ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionSlotRequest request)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await sessionSlotService.CreateAsync(coachId, request);
        return StatusCode(201, new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSessionSlotRequest request)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await sessionSlotService.UpdateAsync(coachId, id, request);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        await sessionSlotService.DeleteAsync(coachId, id);
        return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
    }
}
