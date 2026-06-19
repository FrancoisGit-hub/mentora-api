using Mentora.Core.DTOs.Conversation;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

[ApiController]
[Route("api/v1/member/coaches/{coachId:guid}/conversation")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Conversation")]
public sealed class MemberConversationController(IConversationService service) : ControllerBase
{
    /// <summary>
    /// Returns the member's conversation with the specified coach, creating it on first call (lazy creation).
    /// </summary>
    /// <remarks>
    /// On first call by a member who has never opened the messaging UI for this coach, a new conversation row is
    /// inserted (with no messages yet). Subsequent calls return the existing conversation, including the latest
    /// message if any. Concurrent first-calls for the same (member, coach) pair are handled safely via a
    /// UNIQUE database constraint — exactly one row is ever created.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDto>> Get(
        [FromRoute] Guid coachId,
        CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result = await service.GetOrCreateForMemberAsync(memberId, coachId, ct);
        return Ok(result);
    }
}
