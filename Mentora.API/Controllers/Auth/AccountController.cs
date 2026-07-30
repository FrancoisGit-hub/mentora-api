using System.Security.Claims;
using Mentora.Core.DTOs.Account;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Auth;

/// <summary>
/// Lets the authenticated user request or cancel deletion of their own account. This exists to
/// satisfy App Store guideline 5.1.1(v) (apps offering account creation must let users initiate
/// account deletion) and GDPR's right to erasure. It only records the request for support to
/// process manually — no data is deleted synchronously by this controller.
/// </summary>
[ApiController]
[Route("api/v1/account")]
[Authorize]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Account")]
public class AccountController(IAccountService accountService) : ControllerBase
{
    /// <summary>
    /// Requests deletion of the authenticated user's account. If a request is already pending,
    /// only the reason is updated — the original request date is preserved.
    /// </summary>
    [HttpPost("deletion-request")]
    public async Task<IActionResult> RequestDeletion([FromBody] AccountDeletionRequestDto request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await accountService.RequestDeletionAsync(userId, request, ct);
        return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
    }

    /// <summary>Cancels a pending deletion request for the authenticated user.</summary>
    [HttpDelete("deletion-request")]
    public async Task<IActionResult> CancelDeletion(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await accountService.CancelDeletionRequestAsync(userId, ct);
        return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
    }
}
