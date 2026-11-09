using Mentora.API.Authorization;
using Mentora.Core.DTOs.Admin.Auth;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mentora.API.Controllers.Admin;

/// <summary>
/// Lot 12.1 — admin back-office login for the Mentora associates (ADMIN_USERS, own JWT scheme).
/// Every route is rate limited per client IP. Errors go through GlobalExceptionMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/admin/auth")]
[ApiExplorerSettings(GroupName = AdminAuthDefaults.SwaggerDoc)]
[EnableRateLimiting(AdminAuthDefaults.RateLimitPolicy)]
[Tags("Admin — Auth")]
public class AdminAuthController(IAdminAuthService adminAuthService) : ControllerBase
{
    /// <summary>
    /// Sends a 6-digit login code by mail when the address belongs to an enabled admin.
    /// Always the same 200, whether the address is known or not.
    /// </summary>
    [HttpPost("otp/request")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestCode([FromBody] AdminRequestCodeRequest request, CancellationToken ct)
    {
        await adminAuthService.RequestCodeAsync(request, ct);
        return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Exchanges the code for an access token and a refresh token. A wrong, expired or exhausted
    /// code, an unknown or disabled address all give the same 400 "Invalid or expired code.".
    /// </summary>
    [HttpPost("otp/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyCode([FromBody] AdminVerifyCodeRequest request, CancellationToken ct)
    {
        var result = await adminAuthService.VerifyCodeAsync(request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Rotates the refresh token (single use) and returns a new pair. Any failure gives the same
    /// 401 "Invalid or expired refresh token.".
    /// </summary>
    [HttpPost("token/refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] AdminRefreshRequest request, CancellationToken ct)
    {
        var result = await adminAuthService.RefreshAsync(request, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Revokes the refresh token. Always 200, even for an unknown, malformed or already revoked
    /// token. The access token stays valid until it expires.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] AdminLogoutRequest request, CancellationToken ct)
    {
        await adminAuthService.LogoutAsync(request, ct);
        return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
    }

    /// <summary>The admin behind the access token. 401 when the admin has been disabled.</summary>
    [HttpGet("me")]
    [Authorize(Policy = AdminAuthDefaults.Policy)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst(AdminAuthDefaults.AdminIdClaim)?.Value, out var adminUserId))
            throw new UnauthorizedException("Admin account not found or disabled.");

        var result = await adminAuthService.GetMeAsync(adminUserId, ct);
        return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
    }
}
