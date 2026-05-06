using Mentora.Core.DTOs.Session;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>Manages session visibility and cancellations for the authenticated coach.</summary>
[ApiController]
[Route("api/v1/coach/sessions")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Sessions")]
public class CoachSessionsController(ISessionService sessionService) : ControllerBase
{
    /// <summary>
    /// Lists sessions for the authenticated coach, ordered by scheduled date ascending.
    /// Supports optional status and date-range filters for calendar views.
    /// </summary>
    /// <param name="status">
    /// Optional status filter. Accepted wire values: SCHEDULED, COMPLETED, CANCELLED, UPCOMING, PAST.
    /// UPCOMING = DB Scheduled and ScheduledAt &gt; now (coach's next sessions).
    /// COMPLETED = effective status Completed (includes lazy DB-Scheduled-but-past rows).
    /// </param>
    /// <param name="fromDate">Optional lower bound on SessionScheduledAt (ISO 8601).</param>
    /// <param name="toDate">Optional upper bound on SessionScheduledAt (ISO 8601).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of sessions matching the filters, ordered by scheduled date ascending.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);

        SessionStatusFilter? statusFilter = null;
        if (status is not null)
        {
            try { statusFilter = EnumMappings.SessionStatusFilterMapping.Parse(status); }
            catch (ArgumentOutOfRangeException)
            {
                throw new InvalidOperationException(
                    $"Invalid status value '{status}'. Valid values: {string.Join(", ", EnumMappings.SessionStatusFilterMapping.WireValues)}");
            }
        }

        var result = await sessionService.ListForCoachAsync(coachId, statusFilter, fromDate, toDate, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Returns a single session by ID, verifying it belongs to the authenticated coach.</summary>
    /// <param name="sessionId">Unique identifier of the session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The session details with the effective status computed at read time.</returns>
    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid sessionId, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await sessionService.GetForCoachAsync(coachId, sessionId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Cancels a scheduled session on behalf of the coach.</summary>
    /// <param name="sessionId">Unique identifier of the session to cancel.</param>
    /// <param name="request">Cancellation reason (required, max 500 chars).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated session with status CANCELLED.</returns>
    /// <remarks>
    /// 409 cases: session effective status is not SCHEDULED (already Completed or Cancelled).
    /// Coach cancellations always return the voucher to AVAILABLE regardless of deadline,
    /// since the coach is considered at fault.
    /// </remarks>
    [HttpPost("{sessionId:guid}/cancel")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid sessionId, [FromBody] CancelSessionRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await sessionService.CancelByCoachAsync(coachId, sessionId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
