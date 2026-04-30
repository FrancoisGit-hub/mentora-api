using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

[ApiController]
[Route("api/v1/session-slots")]
[Authorize(Policy = "MemberOnly")]
public class MemberSessionSlotsController(ISessionSlotService sessionSlotService) : ControllerBase
{
    /// <summary>
    /// Returns available session slots from the member's primary coach.
    /// Both 'from' and 'to' are required; the window is capped at 31 days.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailable([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await sessionSlotService.GetAvailableSlotsForMemberAsync(memberId, from, to);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
