using Mentora.Core.DTOs.Catalog;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Coach;

/// <summary>Manages the product catalog for the authenticated coach.</summary>
[ApiController]
[Route("api/v1/coach/products")]
[Authorize(Policy = "CoachOnly")]
[ApiExplorerSettings(GroupName = "coach")]
[Tags("Coach — Catalog")]
public class CoachProductsController(IProductService productService) : ControllerBase
{
    /// <summary>Lists all products owned by the authenticated coach, ordered by most recently updated.</summary>
    /// <returns>A list of all products belonging to this coach regardless of status.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productService.GetByCoachAsync(coachId);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Returns a single product by ID, verifying it belongs to the authenticated coach.</summary>
    /// <param name="productId">Unique identifier of the product.</param>
    /// <returns>The full product details.</returns>
    [HttpGet("{productId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid productId)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productService.GetByIdAsync(productId, coachId);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Creates a new product in the coach's catalog with an initial status of DRAFT.</summary>
    /// <returns>The newly created product. The Location header points to the resource URL.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] ProductRequest request)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productService.CreateAsync(request, coachId);
        return Created(
            $"/api/v1/coach/products/{result.ProductId}",
            new { success = true, data = result, error = (string?)null, statusCode = 201 });
    }

    /// <summary>
    /// Fully replaces all fields of an existing product. The product must belong to the
    /// authenticated coach and must not be archived.
    /// </summary>
    /// <param name="productId">Unique identifier of the product to update.</param>
    /// <param name="request">The replacement product data.</param>
    /// <returns>The updated product.</returns>
    [HttpPut("{productId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid productId, [FromBody] ProductRequest request)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        var result  = await productService.UpdateAsync(productId, request, coachId);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Archives (soft-deletes) a product. Fails if the product is referenced by an active
    /// pack or is already archived.
    /// </summary>
    /// <param name="productId">Unique identifier of the product to archive.</param>
    [HttpDelete("{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid productId)
    {
        var coachId = Guid.Parse(User.FindFirst("coachId")!.Value);
        await productService.DeleteAsync(productId, coachId);
        return NoContent();
    }
}
