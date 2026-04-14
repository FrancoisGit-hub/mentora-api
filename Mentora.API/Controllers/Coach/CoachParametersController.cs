using System.Security.Claims;
using Mentora.Core.DTOs.Coach;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

[ApiController]
[Route("api/v1/coach/parameters")]
[Authorize]
public class CoachParametersController(ICoachParameterService coachParameterService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetParameters()
    {
        try
        {
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await coachParameterService.GetParametersAsync(coachId);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }

    [HttpPut]
    public async Task<IActionResult> UpdateParameters([FromBody] UpdateCoachParameterRequest request)
    {
        try
        {
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await coachParameterService.UpdateParametersAsync(coachId, request);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }
}
