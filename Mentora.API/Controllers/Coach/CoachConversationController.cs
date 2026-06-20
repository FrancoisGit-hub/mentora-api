using Mentora.Core.DTOs.Conversation;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

[ApiController]
[Route("api/v1/coach/members/{memberId:guid}/conversation")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Conversation")]
public sealed class CoachConversationController(IConversationService service) : ControllerBase
{
    /// <summary>
    /// Returns the coach's conversation with the specified member, creating it on first call (lazy creation).
    /// </summary>
    /// <remarks>
    /// On first call, a new conversation row is inserted with no messages. Subsequent calls return the existing
    /// conversation, including the latest message if any.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDto>> Get(
        [FromRoute] Guid memberId,
        CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result = await service.GetOrCreateForCoachAsync(coachId, memberId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Sets the visio URL for the conversation between the authenticated coach and the specified member.
    /// Pass { "url": null } to reset to the auto-generated Jitsi URL.
    /// </summary>
    /// <remarks>
    /// Validation: url must be a valid https:// URL, max 2000 characters, or null.
    /// No domain whitelist — the coach is trusted to provide a meaningful link for their member.
    /// </remarks>
    [HttpPut("visio-url")]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDto>> SetVisioUrl(
        [FromRoute] Guid memberId,
        [FromBody] SetVisioUrlRequestDto body,
        CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result = await service.SetVisioUrlAsync(coachId, memberId, body.Url, ct);
        return Ok(result);
    }
}
