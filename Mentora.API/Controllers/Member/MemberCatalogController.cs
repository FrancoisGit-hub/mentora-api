using Mentora.Core.DTOs.Catalog;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

[ApiController]
[Route("api/v1/member/coaches/{coachId:guid}/catalog")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Catalog")]
public sealed class MemberCatalogController(IMemberCatalogService service) : ControllerBase
{
    /// <summary>
    /// Returns the PUBLISHED catalog (standalone products + packs with composition) of a coach the member is linked to.
    /// </summary>
    /// <remarks>
    /// Pack filtering semantics: when offerType or offerProgramId is provided, a pack is kept if at least one of its items
    /// matches the filter. The pack composition returned is always the full one — items are not trimmed.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(MemberCatalogResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MemberCatalogResponseDto>> GetCatalog(
        [FromRoute] Guid coachId,
        [FromQuery] OfferType? offerType,
        [FromQuery] Guid? offerProgramId,
        CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result = await service.GetCatalogAsync(memberId, coachId, offerType, offerProgramId, ct);
        return Ok(result);
    }
}
