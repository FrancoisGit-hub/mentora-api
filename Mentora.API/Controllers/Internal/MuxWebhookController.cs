using Mentora.Core.DTOs.Mux;
using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Mentora.API.Controllers.Internal;

/// <summary>
/// Lot 12.3a — MUX webhook (anonymous: authenticated by the Mux-Signature HMAC, checked on the raw bytes before
/// any JSON parsing). No rate limit: MUX retries for 24 h on any non-2xx answer.
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
[ApiExplorerSettings(GroupName = "internal")]
[Tags("System — MUX")]
public class MuxWebhookController(IMuxWebhookService webhookService, IOptions<MuxOptions> options) : ControllerBase
{
    public const string SignatureHeader = "Mux-Signature";

    /// <summary>
    /// Receives one MUX event. 401 (empty body) on a missing, malformed, stale or wrong signature; 400 on a body
    /// that is not a MUX event; 200 <c>{ duplicate: true }</c> when the event id was already recorded;
    /// otherwise 200 <c>{ processed: true }</c> or <c>{ ignored: true }</c>. 503 when MUX is not configured.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("mux")]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        if (!options.Value.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, Error("MUX_NOT_CONFIGURED", 503));

        byte[] rawBody;
        try
        {
            using var buffer = new MemoryStream();
            await Request.Body.CopyToAsync(buffer, ct);
            rawBody = buffer.ToArray();
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, Error("Payload too large.", 413));
        }

        var header = Request.Headers.TryGetValue(SignatureHeader, out var values) ? values.ToString() : null;
        var result = await webhookService.HandleAsync(rawBody, header, ct);

        if (result.Outcome == MuxWebhookOutcome.Rejected)
        {
            // EmptyResult, not StatusCode(401): [ApiController] would turn a bare status into a ProblemDetails body.
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return new EmptyResult();
        }
        if (result.Outcome == MuxWebhookOutcome.InvalidPayload)
            return BadRequest(Error(result.Result ?? "Invalid MUX event payload.", 400));
        if (result.Outcome == MuxWebhookOutcome.Duplicate)
            return Ok(Success(new { duplicate = true }));
        if (result.Outcome == MuxWebhookOutcome.Processed)
            return Ok(Success(new { processed = true }));
        return Ok(Success(new { ignored = true }));
    }

    private static object Success(object data) =>
        new { success = true, data, error = (string?)null, statusCode = 200 };

    private static object Error(string error, int statusCode) =>
        new { success = false, data = (object?)null, error, statusCode };
}
