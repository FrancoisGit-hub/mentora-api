using System.Security.Claims;
using Mentora.Core.DTOs.Sessions;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

[ApiController]
[Route("api/v1/coach/session-slots")]
[Authorize]
public class SessionSlotsAdminController(ISessionSlotService sessionSlotService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateSlot([FromBody] CreateSessionSlotRequest request)
    {
        try
        {
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await sessionSlotService.CreateSlotAsync(coachId, request);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }

    [HttpPut("{slotId:guid}")]
    public async Task<IActionResult> UpdateSlot(Guid slotId, [FromBody] CreateSessionSlotRequest request)
    {
        try
        {
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await sessionSlotService.UpdateSlotAsync(coachId, slotId, request);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("booked", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { success = false, data = (object?)null, error = ex.Message, statusCode = 409 });
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }

    [HttpDelete("{slotId:guid}")]
    public async Task<IActionResult> DeleteSlot(Guid slotId)
    {
        try
        {
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            await sessionSlotService.DeleteSlotAsync(coachId, slotId);
            return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("booked", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { success = false, data = (object?)null, error = ex.Message, statusCode = 409 });
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }
}
