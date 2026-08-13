using Mentora.Core.DTOs.Agenda;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Merged calendar of the authenticated member's booked sessions and standalone program sessions.</summary>
[ApiController]
[Route("api/v1/member/agenda")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Agenda")]
public class MemberAgendaController(IAgendaService agendaService) : ControllerBase
{
    /// <summary>
    /// Returns everything, merged and sorted by date: booked sessions (individual and group) and
    /// standalone program sessions that carry no booking yet.
    /// </summary>
    /// <param name="from">Inclusive lower bound. Required.</param>
    /// <param name="to">Inclusive upper bound. Required. Range capped at 186 days.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await agendaService.GetForMemberAsync(memberId, new MemberAgendaRequest(from, to), ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
