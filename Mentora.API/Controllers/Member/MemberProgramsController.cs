using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Provides read-only access to the authenticated member's assigned programs.</summary>
[ApiController]
[Route("api/v1/member/programs")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Programs")]
public class MemberProgramsController(IProgramService programService) : ControllerBase
{
    /// <summary>Returns the authenticated member's active program, full tree included.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("current")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await programService.GetCurrentForMemberAsync(memberId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Returns one of the authenticated member's own programs, active or archived. Another
    /// member's program id 404s.
    /// </summary>
    /// <param name="programId">Unique identifier of the program.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{programId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid programId, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await programService.GetByIdForMemberAsync(memberId, programId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
