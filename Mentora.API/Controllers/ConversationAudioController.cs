using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers;

/// <summary>
/// Lot 13.4 (13.B): listening route of voice messages, shared by the member and the coach of a conversation.
/// Plain [Authorize] like ChatHub: the default scheme is the app Bearer scheme, so an admin token is refused.
/// </summary>
[ApiController]
[Route("api/v1/conversations/{conversationId:guid}/messages/{messageId:guid}/audio")]
[Authorize]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Conversation: audio")]
public sealed class ConversationAudioController(
    IConversationService service,
    IVoiceMessageStorage storage,
    ILogger<ConversationAudioController> logger) : ControllerBase
{
    /// <summary>
    /// Streams the audio file of a voice message to a participant of the conversation (the mediaUrl of the
    /// message). Supports Range requests (206). Binary response, no envelope.
    /// </summary>
    /// <param name="conversationId">Conversation of the message.</param>
    /// <param name="messageId">The voice message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The whole file (audio/aac or audio/mp4).</response>
    /// <response code="206">The requested byte range, with Content-Range.</response>
    /// <response code="401">No valid app token.</response>
    /// <response code="404">Unknown or not accessible (same empty response for every case).</response>
    /// <response code="503">Audio storage is not configured on this server (AUDIO_NOT_CONFIGURED).</response>
    [HttpGet]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, "audio/aac", "audio/mp4")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status206PartialContent, "audio/aac", "audio/mp4")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(
        [FromRoute] Guid conversationId,
        [FromRoute] Guid messageId,
        CancellationToken ct)
    {
        var callerMemberId = ClaimGuid("memberId");
        var callerCoachId = ClaimGuid("coachId");

        var audio = await service.GetAudioFileForParticipantAsync(
            conversationId, messageId, callerMemberId, callerCoachId, ct);
        if (audio is null)
            return EmptyNotFound();

        var (relativePath, contentType) = audio.Value;
        if (!storage.Exists(relativePath))
        {
            logger.LogWarning("[AUDIO] stored file missing for message {MessageId}", messageId);
            return EmptyNotFound();
        }

        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(storage.OpenRead(relativePath), contentType, enableRangeProcessing: true);
    }

    private Guid? ClaimGuid(string claimType)
        => Guid.TryParse(User.FindFirst(claimType)?.Value, out var value) ? value : null;

    // EmptyResult, not NotFound(): [ApiController] would add a ProblemDetails body with a per request traceId,
    // and every 404 of this route must be identical.
    private EmptyResult EmptyNotFound()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return new EmptyResult();
    }
}
