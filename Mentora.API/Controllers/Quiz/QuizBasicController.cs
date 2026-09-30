using Mentora.API.Authorization;
using Mentora.Core.DTOs.Quiz;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Mentora.API.Controllers.Quiz;

/// <summary>
/// Basic quiz. Works for a coach (JWT) and, without any Authorization header, for an anonymous
/// device identified by <c>X-Device-Id</c>. Anonymous requests are rate limited per client IP.
/// </summary>
[ApiController]
[Route("api/v1/quiz/basic")]
[Authorize(Policy = "QuizPlayer")]
[EnableRateLimiting(QuizRateLimiting.PolicyName)]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Quiz")]
public class QuizBasicController(IQuizBasicService quizService) : ControllerBase
{
    /// <summary>Draws a new quiz session (random active questions) for the caller.</summary>
    /// <param name="deviceId">Device mode only (no Authorization header): 16 to 64 characters, ASCII letters, digits and hyphen.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(QuizSessionEnvelope), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateSession(
        [FromHeader(Name = QuizRateLimiting.DeviceIdHeader)] string? deviceId, CancellationToken ct)
    {
        var result = await quizService.CreateSessionAsync(ResolveOwner(deviceId), ct);
        return StatusCode(201, new QuizSessionEnvelope(true, result, null, 201));
    }

    /// <summary>Submits all the answers of a session; the score is computed by the API.</summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="request">One answer per drawn question.</param>
    /// <param name="deviceId">Device mode only (no Authorization header).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("sessions/{sessionId:guid}/answers")]
    [ProducesResponseType(typeof(QuizSubmitEnvelope), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit(
        Guid sessionId, [FromBody] QuizSubmitRequest request,
        [FromHeader(Name = QuizRateLimiting.DeviceIdHeader)] string? deviceId, CancellationToken ct)
    {
        var result = await quizService.SubmitAsync(ResolveOwner(deviceId), sessionId, request, ct);
        return Ok(new QuizSubmitEnvelope(true, result, null, 200));
    }

    /// <summary>Completed sessions of the caller with totals, newest first (cursor pagination).</summary>
    /// <param name="limit">Page size, 1 to 50 (default 20).</param>
    /// <param name="cursor">Opaque cursor returned as <c>nextCursor</c>.</param>
    /// <param name="deviceId">Device mode only (no Authorization header).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("me")]
    [ProducesResponseType(typeof(QuizHistoryEnvelope), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Me(
        [FromQuery] int limit = 20, [FromQuery] string? cursor = null,
        [FromHeader(Name = QuizRateLimiting.DeviceIdHeader)] string? deviceId = null, CancellationToken ct = default)
    {
        var result = await quizService.GetHistoryAsync(ResolveOwner(deviceId), limit, cursor, ct);
        return Ok(new QuizHistoryEnvelope(true, result, null, 200));
    }

    /// <summary>
    /// Coach mode as soon as an Authorization header is present (the QuizPlayer policy has then
    /// already required a valid coach token); device mode otherwise. X-Device-Id is ignored in coach mode.
    /// </summary>
    private QuizOwner ResolveOwner(string? deviceId) =>
        Request.Headers.ContainsKey(HeaderNames.Authorization)
            ? new QuizOwner(Guid.Parse(User.FindFirst("coachId")!.Value), null)
            : new QuizOwner(null, deviceId);
}
