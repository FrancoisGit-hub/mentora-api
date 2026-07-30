using Mentora.Core.DTOs.Member;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Manages the authenticated member's own settings: contact/physical details and preferences.</summary>
[ApiController]
[Route("api/v1/member/parameters")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Parameters")]
public class MemberParametersController(IMemberParameterService parameterService) : ControllerBase
{
    /// <summary>Returns the authenticated member's identity, physical details, and preferences.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The member's parameters. A default-valued row is created on first access if none exists yet.</returns>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await parameterService.GetAsync(memberId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Fully replaces the authenticated member's own writable settings: phone, gender, height,
    /// birth date, language, and notification preferences.
    /// </summary>
    /// <remarks>
    /// FirstName, LastName, and Email are NOT modifiable through this endpoint — changing a
    /// member's identity fields is a support-only operation, by design.
    /// </remarks>
    /// <param name="request">The replacement parameter values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated member parameters.</returns>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateMemberParameterRequest request, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await parameterService.UpdateAsync(memberId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
