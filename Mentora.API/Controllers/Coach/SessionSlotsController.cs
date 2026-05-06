using Mentora.Core.DTOs.Lot2;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

[ApiController]
[Route("api/v1/coach/session-slots")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Session Slots")]
public class SessionSlotsController(ISessionSlotService sessionSlotService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionSlotRequest request)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await sessionSlotService.CreateAsync(coachId, request);
        return StatusCode(201, new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSessionSlotRequest request)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await sessionSlotService.UpdateAsync(coachId, id, request);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        await sessionSlotService.DeleteAsync(coachId, id);
        return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
    }
}
