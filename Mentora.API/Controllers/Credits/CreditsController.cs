using System.Security.Claims;
using Mentora.Core.DTOs.Credits;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Credits;

[ApiController]
[Route("api/v1/credits")]
[Authorize]
public class CreditsController(ICreditService creditService) : ControllerBase
{
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance()
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await creditService.GetBalanceAsync(userId, coachId);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await creditService.GetTransactionsAsync(userId, coachId, page, size);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> PurchaseCredits([FromBody] PurchaseCreditsRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var coachId = Guid.Parse(User.FindFirstValue("coachId")!);
            var result = await creditService.PurchaseCreditsAsync(userId, coachId, request.Quantity);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }
}
