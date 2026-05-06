using Mentora.Core.DTOs.Session;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Manages session reservations and cancellations for the authenticated member.</summary>
[ApiController]
[Route("api/v1/member/sessions")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Sessions")]
public class MemberSessionsController(ISessionService sessionService) : ControllerBase
{
    /// <summary>Reserves a session slot by consuming an available voucher.</summary>
    /// <param name="request">The voucher and slot to reserve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created session.</returns>
    /// <remarks>
    /// 409 cases: voucher not Available, slot already booked by a non-cancelled session,
    /// slot does not belong to the voucher's coach, voucher and slot are incompatible
    /// (offerType or durationMinutes mismatch), slot is in the past,
    /// PresentielGroupe offer type (not supported in V1), or a concurrent reservation
    /// race condition (Postgres serialization failure — client should retry).
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reserve([FromBody] ReserveSessionRequest request, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await sessionService.ReserveAsync(memberId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Lists sessions for the authenticated member, ordered by scheduled date ascending.</summary>
    /// <param name="status">
    /// Optional status filter. Accepted wire values: SCHEDULED, COMPLETED, CANCELLED, UPCOMING, PAST.
    /// SCHEDULED = in-progress or future with effective status Scheduled.
    /// UPCOMING = Scheduled and not yet started (ScheduledAt &gt; now).
    /// COMPLETED = effective status Completed (includes lazy DB-Scheduled-but-past rows).
    /// PAST = Completed or Cancelled.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of sessions matching the filter, or all sessions if no filter is provided.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] string? status, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);

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

        var result = await sessionService.ListForMemberAsync(memberId, statusFilter, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Returns a single session by ID, verifying it belongs to the authenticated member.</summary>
    /// <param name="sessionId">Unique identifier of the session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The session details with the effective status computed at read time.</returns>
    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid sessionId, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await sessionService.GetForMemberAsync(memberId, sessionId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Cancels a scheduled session on behalf of the member.</summary>
    /// <param name="sessionId">Unique identifier of the session to cancel.</param>
    /// <param name="request">Cancellation reason (required, max 500 chars).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated session with status CANCELLED.</returns>
    /// <remarks>
    /// 409 cases: session effective status is not SCHEDULED (already Completed or Cancelled).
    /// Voucher fate depends on the cancellation deadline set by the coach:
    /// cancelled before the deadline → voucher returns to AVAILABLE (member may rebook);
    /// cancelled after the deadline → voucher is CONSUMED (late cancellation penalty).
    /// </remarks>
    [HttpPost("{sessionId:guid}/cancel")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid sessionId, [FromBody] CancelSessionRequest request, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await sessionService.CancelByMemberAsync(memberId, sessionId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
