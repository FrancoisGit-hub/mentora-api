using System.Security.Claims;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Sessions;

[ApiController]
[Route("api/v1/session-slots")]
[Authorize]
public class SessionSlotsController(ISessionSlotService sessionSlotService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAvailableSlots([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        try
        {
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await sessionSlotService.GetAvailableSlotsAsync(coachId, from, to);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }
}
