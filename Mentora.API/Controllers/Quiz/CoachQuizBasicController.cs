using Mentora.Core.DTOs.Quiz;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Quiz;

/// <summary>Lets a coach take over the quiz points earned anonymously on a device.</summary>
[ApiController]
[Route("api/v1/coach/quiz/basic")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Quiz")]
public class CoachQuizBasicController(IQuizDeviceClaimService claimService) : ControllerBase
{
    /// <summary>
    /// Attributes the sessions of an anonymous device to the calling coach. One claim per device,
    /// ever; a retry by the same coach returns the existing claim. A coach may claim several devices.
    /// </summary>
    /// <param name="request">The device to claim.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("claim-device")]
    [ProducesResponseType(typeof(QuizClaimDeviceEnvelope), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ClaimDevice([FromBody] QuizClaimDeviceRequest request, CancellationToken ct)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await claimService.ClaimAsync(coachId, request, ct);
        return Ok(new QuizClaimDeviceEnvelope(true, result, null, 200));
    }
}
