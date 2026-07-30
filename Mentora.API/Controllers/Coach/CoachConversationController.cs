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
}
