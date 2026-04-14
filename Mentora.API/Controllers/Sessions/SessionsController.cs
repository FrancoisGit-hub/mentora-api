using System.Security.Claims;
using Mentora.Core.DTOs.Sessions;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Sessions;

[ApiController]
[Route("api/v1/sessions")]
[Authorize]
public class SessionsController(ISessionService sessionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSessions([FromQuery] string? status)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await sessionService.GetSessionsAsync(userId, coachId, status);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> GetSessionById(Guid sessionId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await sessionService.GetSessionByIdAsync(userId, coachId, sessionId);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }

    [HttpPost("book")]
    public async Task<IActionResult> BookSession([FromBody] BookSessionRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await sessionService.BookSessionAsync(userId, coachId, request.SlotId);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already booked", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { success = false, data = (object?)null, error = ex.Message, statusCode = 409 });
            if (ex.Message.Contains("Insufficient", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }

    [HttpDelete("{sessionId:guid}")]
    public async Task<IActionResult> CancelSession(Guid sessionId, [FromBody] CancelSessionRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await sessionService.CancelSessionAsync(userId, coachId, sessionId, request.Reason);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("deadline", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("CONFIRMED", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }
}
