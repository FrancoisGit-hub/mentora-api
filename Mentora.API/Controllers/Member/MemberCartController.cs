using Mentora.Core.DTOs.Cart;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Member;

/// <summary>Manages the shopping cart for the authenticated member.</summary>
[ApiController]
[Route("api/v1/member/coaches/{coachId:guid}/cart")]
[Authorize(Policy = "MemberOnly")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Member — Cart")]
public class MemberCartController(ICartService cartService) : ControllerBase
{
    /// <summary>Returns the cart the authenticated member has with a specific coach.</summary>
    /// <param name="coachId">Coach whose cart to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cart (possibly empty) for this member–coach pair.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid coachId, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await cartService.GetForCoachAsync(memberId, coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Adds a product or pack to the cart. Creates the cart if it does not yet exist.</summary>
    /// <param name="coachId">Coach whose cart to add to.</param>
    /// <param name="request">The item to add (exactly one of ProductId or PackId must be set).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated cart.</returns>
    [HttpPost("items")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddItem(Guid coachId, [FromBody] AddCartItemRequest request, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await cartService.AddItemAsync(memberId, coachId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Updates the quantity of an existing cart item.</summary>
    /// <param name="coachId">Coach whose cart to update.</param>
    /// <param name="cartItemId">The cart item to update.</param>
    /// <param name="request">New quantity (1–99).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated cart.</returns>
    [HttpPut("items/{cartItemId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItemQuantity(
        Guid coachId, Guid cartItemId, [FromBody] UpdateCartItemQuantityRequest request, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await cartService.UpdateItemQuantityAsync(memberId, coachId, cartItemId, request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Removes a single item from the cart.</summary>
    /// <param name="coachId">Coach whose cart to modify.</param>
    /// <param name="cartItemId">The cart item to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated cart.</returns>
    [HttpDelete("items/{cartItemId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(Guid coachId, Guid cartItemId, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await cartService.RemoveItemAsync(memberId, coachId, cartItemId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Removes all items from the cart without placing an order.</summary>
    /// <param name="coachId">Coach whose cart to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The (now empty) cart.</returns>
    [HttpDelete("items")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Clear(Guid coachId, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await cartService.ClearAsync(memberId, coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Converts the cart into a PENDING order and returns a Stripe Checkout URL.
    /// The cart is cleared on success.
    /// </summary>
    /// <param name="coachId">Coach whose cart to check out.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created order ID and the Stripe checkout URL.</returns>
    /// <remarks>Returns 400 if the cart is empty or contains unavailable items.</remarks>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Checkout(Guid coachId, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("memberId")!.Value);
        var result   = await cartService.CheckoutAsync(memberId, coachId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
