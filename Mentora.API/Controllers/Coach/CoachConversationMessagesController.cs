using Mentora.API.Authorization;
using Mentora.API.Hubs;
using Mentora.Core.DTOs.Conversation;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Mentora.Core.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace Mentora.API.Controllers.Coach;

[ApiController]
[Route("api/v1/coach/members/{memberId:guid}/conversation/messages")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Conversation")]
public sealed class CoachConversationMessagesController(
    IConversationService service,
    IHubContext<ChatHub> hubContext,
    ILogger<CoachConversationMessagesController> logger) : ControllerBase
{
    /// <summary>
    /// Returns the messages of the conversation between the authenticated coach and the specified member.
    /// Cursor-based pagination, most-recent-first ordering.
    /// </summary>
    /// <param name="memberId">The member.</param>
    /// <param name="before">Optional cursor — return messages with SentDate strictly before this value.</param>
    /// <param name="limit">Max messages per page. Default 50, max 100.</param>
    [HttpGet]
    [ProducesResponseType(typeof(MessageListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageListResponseDto>> List(
        [FromRoute] Guid memberId,
        [FromQuery] DateTime? before,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var effectiveLimit = Math.Clamp(limit ?? 50, 1, 100);
        var result = await service.GetMessagesAsync(memberId, coachId, before, effectiveLimit, ct);
        return Ok(result);
    }

    /// <summary>
    /// Sends a new message from the authenticated coach to the specified member.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SendMessageResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SendMessageResponseDto>> Send(
        [FromRoute] Guid memberId,
        [FromBody] SendMessageRequestDto body,
        CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var message = await service.SendMessageAsync(
            memberId, coachId, MessageSenderType.Coach, coachId, body.Content, ct);
        await BroadcastAsync(message);
        return CreatedAtAction(nameof(List), new { memberId }, new SendMessageResponseDto(message));
    }

    /// <summary>
    /// Sends a voice message from the authenticated coach to the specified member (multipart/form-data).
    /// The server checks the format (AAC in ADTS or M4A), measures the duration (0.3 s to 120 s) and computes
    /// the waveform; the client duration is ignored. Broadcast as ReceiveMessage to the conversation group.
    /// </summary>
    /// <param name="memberId">The member.</param>
    /// <param name="file">The recording (maximum 5 MB).</param>
    /// <param name="durationMs">Duration measured by the client, accepted and ignored (the server measures it).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">The voice message, same shape as the text send (no envelope).</response>
    /// <response code="404">The coach is not linked to this member.</response>
    /// <response code="413">The request body is larger than 8 MB.</response>
    /// <response code="415">The request is not multipart/form-data.</response>
    /// <response code="422">File missing, too large, unsupported, too short, too long or undecodable (key File).</response>
    /// <response code="429">More than 30 uploads in 10 minutes for this user.</response>
    /// <response code="503">Audio is not configured on this server (AUDIO_NOT_CONFIGURED).</response>
    [HttpPost("audio")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(VoiceMessageRules.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = VoiceMessageRules.MaxRequestBytes)]
    [EnableRateLimiting(VoiceUploadRateLimiting.PolicyName)]
    [ProducesResponseType(typeof(SendMessageResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SendMessageResponseDto>> SendAudio(
        [FromRoute] Guid memberId,
        [FromForm] IFormFile? file,
        [FromForm] int? durationMs,
        CancellationToken ct)
    {
        if (await IsRequestBodyTooLargeAsync(ct))
            return StatusCode(StatusCodes.Status413PayloadTooLarge, PayloadTooLarge());

        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        await using var content = file?.OpenReadStream();
        var message = await service.SendAudioMessageAsync(
            memberId, coachId, MessageSenderType.Coach, coachId, content, file?.Length, ct);
        await BroadcastAsync(message);
        return CreatedAtAction(nameof(List), new { memberId }, new SendMessageResponseDto(message));
    }

    /// <summary>
    /// Marks all unread messages from the member as read.
    /// Returns the number of messages that were actually marked.
    /// </summary>
    [HttpPatch("read")]
    [ProducesResponseType(typeof(MarkAsReadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarkAsReadResponseDto>> MarkAsRead(
        [FromRoute] Guid memberId,
        CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var count = await service.MarkMessagesAsReadAsync(memberId, coachId, MessageSenderType.Coach, ct);
        return Ok(new MarkAsReadResponseDto(count));
    }

    /// <summary>
    /// Lot 13.4 (13.I): same event and payload as ChatHub.SendMessageAsync. The message is already saved, so a
    /// broadcast failure is logged and never changes the HTTP response.
    /// </summary>
    private async Task BroadcastAsync(MessageDto message)
    {
        try
        {
            await hubContext.Clients.Group(ChatHub.GroupName(message.ConversationId))
                .SendAsync("ReceiveMessage", message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning("[CHAT] broadcast failed for message {MessageId}: {Error}",
                message.MessageId, ex.GetType().Name);
        }
    }

    /// <summary>
    /// Lot 13.4: true when the body is above the request size limit. Form binding swallows the server's 413
    /// (the file then binds as null), so the cached form read is replayed to find the original rejection.
    /// </summary>
    private async Task<bool> IsRequestBodyTooLargeAsync(CancellationToken ct)
    {
        if (Request.ContentLength > VoiceMessageRules.MaxRequestBytes)
            return true;
        if (ModelState.IsValid || !Request.HasFormContentType)
            return false;
        try
        {
            await Request.ReadFormAsync(ct);
            return false;
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return false;
        }
    }

    private static object PayloadTooLarge() =>
        new { success = false, data = (object?)null, error = "Payload too large.", statusCode = 413 };
}
