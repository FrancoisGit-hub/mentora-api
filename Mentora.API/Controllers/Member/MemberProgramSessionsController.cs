using Mentora.Core.DTOs.Program;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Lets the member record their own completion entry for a program session.</summary>
[ApiController]
[Route("api/v1/member/program-sessions")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Programs")]
public class MemberProgramSessionsController(IProgramService programService) : ControllerBase
{
    /// <summary>
    /// Full replacement of the session's completion record — resubmitting corrects a previous
    /// entry. Any exercise omitted from the payload has its actuals cleared, not left alone.
    /// Never touches the prescribed columns.
    /// </summary>
    /// <param name="programSessionId">Unique identifier of the program session.</param>
    /// <param name="request">The completion record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// 404 if the program session isn't the authenticated member's own. 409 if the program is not
    /// ACTIVE. 422 for an out-of-session exercise id, an out-of-range RPE, or actualWeightKg on a
    /// non-KG exercise.
    /// </remarks>
    [HttpPut("{programSessionId:guid}/completion")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCompletion(
        Guid programSessionId, [FromBody] UpdateProgramSessionCompletionRequest request, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await programService.UpdateCompletionByMemberAsync(memberId, programSessionId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
