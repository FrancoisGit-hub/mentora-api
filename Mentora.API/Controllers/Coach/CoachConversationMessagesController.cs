using Mentora.Core.DTOs.Conversation;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

[ApiController]
[Route("api/v1/coach/members/{memberId:guid}/conversation/messages")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Conversation")]
public sealed class CoachConversationMessagesController(IConversationService service) : ControllerBase
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
}
