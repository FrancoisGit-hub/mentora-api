using Mentora.Core.DTOs.Program;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>Lets the coach correct which booking a program session is linked to.</summary>
[ApiController]
[Route("api/v1/coach/program-sessions")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Agenda")]
public class CoachProgramSessionsController(IProgramService programService) : ControllerBase
{
    /// <summary>
    /// Links or detaches a program session's booking. Passing a sessionId attaches (or
    /// reassigns) it; passing null detaches — clearing the link without touching the booking
    /// itself (voucher, participant registration, session row all stay as they are).
    /// </summary>
    /// <param name="programSessionId">Unique identifier of the program session to correct.</param>
    /// <param name="request">The booking to link, or null to detach.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// 404 for any ownership mismatch (program session, session, or member), never 403.
    /// 409 if the session's offer type doesn't match the program session's type, or if the
    /// member already has a different program session linked to that same booking.
    /// </remarks>
    [HttpPut("{programSessionId:guid}/booking")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateBooking(
        Guid programSessionId, [FromBody] UpdateProgramSessionBookingRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await programService.UpdateBookingAsync(coachId, programSessionId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
