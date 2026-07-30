using Mentora.Core.DTOs.Lot2;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

[ApiController]
[Route("api/v1/coach/parameters")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Parameters")]
public class CoachParametersController(ICoachParameterService parameterService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await parameterService.GetAsync(coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateCoachParameterRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await parameterService.UpdateAsync(coachId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
