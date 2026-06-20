using Mentora.Core.DTOs.Conversation;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

[ApiController]
[Route("api/v1/member/coaches/{coachId:guid}/conversation/messages")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Conversation")]
public sealed class MemberConversationMessagesController(IConversationService service) : ControllerBase
{
    /// <summary>
    /// Returns the messages of the conversation between the authenticated member and the specified coach.
    /// Cursor-based pagination, most-recent-first ordering.
    /// </summary>
    /// <param name="coachId">The coach.</param>
    /// <param name="before">Optional cursor — return messages with SentDate strictly before this value.</param>
    /// <param name="limit">Max messages per page. Default 50, max 100.</param>
    [HttpGet]
    [ProducesResponseType(typeof(MessageListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageListResponseDto>> List(
        [FromRoute] Guid coachId,
        [FromQuery] DateTime? before,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var effectiveLimit = Math.Clamp(limit ?? 50, 1, 100);
        var result = await service.GetMessagesAsync(memberId, coachId, before, effectiveLimit, ct);
        return Ok(result);
    }

    /// <summary>
    /// Sends a new message from the authenticated member to the specified coach.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SendMessageResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SendMessageResponseDto>> Send(
        [FromRoute] Guid coachId,
        [FromBody] SendMessageRequestDto body,
        CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var message = await service.SendMessageAsync(
            memberId, coachId, MessageSenderType.Member, memberId, body.Content, ct);
        return CreatedAtAction(nameof(List), new { coachId }, new SendMessageResponseDto(message));
    }

    /// <summary>
    /// Marks all unread messages from the coach as read.
    /// Returns the number of messages that were actually marked.
    /// </summary>
    [HttpPatch("read")]
    [ProducesResponseType(typeof(MarkAsReadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarkAsReadResponseDto>> MarkAsRead(
        [FromRoute] Guid coachId,
        CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var count = await service.MarkMessagesAsReadAsync(memberId, coachId, MessageSenderType.Member, ct);
        return Ok(new MarkAsReadResponseDto(count));
    }
}
