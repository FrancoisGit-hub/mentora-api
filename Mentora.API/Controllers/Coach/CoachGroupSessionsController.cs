using Mentora.Core.DTOs.Session;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>
/// Manages group sessions and their participant rosters for the authenticated coach. The coach
/// registers and unregisters members — there is no member-facing write endpoint for group
/// sessions, per product decision.
/// </summary>
[ApiController]
[Route("api/v1/coach/sessions")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Group sessions")]
public class CoachGroupSessionsController(ISessionService sessionService) : ControllerBase
{
    /// <summary>
    /// Creates a group session on an available slot from an explicit product. The slot must
    /// belong to the coach, be available, carry a group offer type, and not already carry a
    /// session. The product must belong to the coach, be published, carry a group offer type,
    /// and match the slot's offer type and duration.
    /// </summary>
    /// <param name="request">The slot and product to create the group session from.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("group")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateGroupSession(
        [FromBody] CreateGroupSessionRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await sessionService.CreateGroupSessionAsync(coachId, request, ct);
        return Created(
            $"/api/v1/coach/sessions/{result.SessionId}",
            new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }

    /// <summary>Lists every registration for a group session, including cancelled ones (kept for history).</summary>
    /// <param name="sessionId">Unique identifier of the group session.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{sessionId:guid}/participants")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListParticipants(Guid sessionId, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await sessionService.ListParticipantsAsync(coachId, sessionId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Registers a member for a group session, consuming one of their vouchers. 404 if the
    /// session isn't the coach's or isn't a group session, or if the member isn't linked to the
    /// coach — never 403. 409 if the voucher is invalid/incompatible or the session is at
    /// capacity.
    /// </summary>
    /// <param name="sessionId">Unique identifier of the group session.</param>
    /// <param name="request">The member to register and the voucher to consume.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{sessionId:guid}/participants")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterParticipant(
        Guid sessionId, [FromBody] RegisterParticipantRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await sessionService.RegisterParticipantAsync(coachId, sessionId, request, ct);
        return StatusCode(201, new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }

    /// <summary>
    /// Unregisters a member from a group session. Sets the registration to CANCELLED (kept for
    /// history) and returns the voucher to Available.
    /// </summary>
    /// <param name="sessionId">Unique identifier of the group session.</param>
    /// <param name="memberId">Unique identifier of the member to unregister.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{sessionId:guid}/participants/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnregisterParticipant(Guid sessionId, Guid memberId, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        await sessionService.UnregisterParticipantAsync(coachId, sessionId, memberId, ct);
        return NoContent();
    }
}
