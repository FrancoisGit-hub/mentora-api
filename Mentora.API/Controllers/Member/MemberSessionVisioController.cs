using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Issues LiveKit video-call access tokens for the authenticated member.</summary>
[ApiController]
[Route("api/v1/member/sessions")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Sessions")]
public class MemberSessionVisioController(IVisioTokenService visioTokenService) : ControllerBase
{
    /// <summary>Issues a LiveKit room-access token for an eligible VISIO session.</summary>
    /// <param name="sessionId">Unique identifier of the session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The LiveKit server URL, a signed token, the room name, and the token's expiration.</returns>
    /// <remarks>
    /// The token is only issued within the join window: from (ScheduledAt - JoinBeforeMinutes)
    /// through (ScheduledAt + DurationMinutes + JoinAfterEndMinutes), both configured under the
    /// LiveKit options section (defaults: 15 minutes before, 15 minutes after).
    ///
    /// 409 reasons (in the response's "details" object):
    ///   NOT_ELIGIBLE    — not a VISIO session, or a group session (no single member — this
    ///                     endpoint is for the individual booking, same scope as .../cancel).
    ///   CANCELLED       — the session has been cancelled.
    ///   WINDOW_NOT_OPEN — requested before the join window opens.
    ///   WINDOW_CLOSED   — requested after the join window closes.
    ///
    /// 404 when the session does not exist or does not belong to the authenticated member.
    /// </remarks>
    [HttpPost("{sessionId:guid}/visio-token")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IssueToken(Guid sessionId, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await visioTokenService.IssueForMemberAsync(memberId, sessionId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
