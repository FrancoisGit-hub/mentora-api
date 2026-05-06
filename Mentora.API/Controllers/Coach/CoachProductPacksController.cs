using Mentora.Core.DTOs.Catalog;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

[ApiController]
[Route("api/v1/coach/product-packs")]
[Authorize(Policy = "CoachOnly")]
public class CoachProductPacksController(IProductPackService productPackService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productPackService.GetByCoachAsync(coachId);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    [HttpGet("{packId:guid}")]
    public async Task<IActionResult> GetById(Guid packId)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productPackService.GetByIdAsync(packId, coachId);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductPackRequest request)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productPackService.CreateAsync(request, coachId);
        return Created(
            $"/api/v1/coach/product-packs/{result.ProductPackId}",
            new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }

    [HttpPut("{packId:guid}")]
    public async Task<IActionResult> Update(Guid packId, [FromBody] ProductPackRequest request)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productPackService.UpdateAsync(packId, request, coachId);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    [HttpDelete("{packId:guid}")]
    public async Task<IActionResult> Delete(Guid packId)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        await productPackService.DeleteAsync(packId, coachId);
        return NoContent();
    }
}
