using Mentora.Core.DTOs.Coach;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

[ApiController]
[Route("api/v1/coach/conversations")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Conversation")]
public sealed class CoachConversationsController(IConversationService service) : ControllerBase
{
    /// <summary>
    /// Returns the coach's aggregated inbox: one entry per member linked to the coach, ordered by
    /// lastMessageDate descending (members with no messages last). Replaces the previous 1+N
    /// pattern of listing members then fetching one conversation per member.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CoachConversationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<CoachConversationSummaryDto>>> GetInbox(CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await service.GetInboxForCoachAsync(coachId, ct);
        return Ok(result);
    }
}
