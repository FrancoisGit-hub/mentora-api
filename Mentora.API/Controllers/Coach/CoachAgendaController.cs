using Mentora.Core.DTOs.Agenda;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>Merged calendar of the authenticated coach's bookings, and optionally their members' standalone program sessions.</summary>
[ApiController]
[Route("api/v1/coach/agenda")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Agenda")]
public class CoachAgendaController(IAgendaService agendaService) : ControllerBase
{
    /// <summary>
    /// Returns, merged and sorted by date: the coach's bookings (individual and group), and —
    /// only when <paramref name="includeMemberStandalone"/> is true — the program sessions of
    /// their members that carry no booking yet.
    /// </summary>
    /// <param name="from">Inclusive lower bound. Required.</param>
    /// <param name="to">Inclusive upper bound. Required. Range capped at 186 days.</param>
    /// <param name="includeMemberStandalone">
    /// Defaults to false: a member's Sunday home workout is not an appointment in the coach's
    /// working day. The coach sees the full program on the member detail screen instead.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] bool includeMemberStandalone = false,
        CancellationToken ct = default)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await agendaService.GetForCoachAsync(
            coachId, new CoachAgendaRequest(from, to, includeMemberStandalone), ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
