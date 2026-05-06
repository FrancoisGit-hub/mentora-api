using Mentora.Core.DTOs.Catalog;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

[ApiController]
[Route("api/v1/coach/products")]
[Authorize(Policy = "CoachOnly")]
public class CoachProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productService.GetByCoachAsync(coachId);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> GetById(Guid productId)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productService.GetByIdAsync(productId, coachId);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductRequest request)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productService.CreateAsync(request, coachId);
        return Created(
            $"/api/v1/coach/products/{result.ProductId}",
            new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }

    [HttpPut("{productId:guid}")]
    public async Task<IActionResult> Update(Guid productId, [FromBody] ProductRequest request)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productService.UpdateAsync(productId, request, coachId);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> Delete(Guid productId)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        await productService.DeleteAsync(productId, coachId);
        return NoContent();
    }
}
