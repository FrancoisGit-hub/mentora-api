using System.Security.Claims;
using Mentora.Core.DTOs.Auth;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Auth;

[ApiController]
[Route("api/v1/auth")]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest request)
    {
        try
        {
            await authService.RequestOtpAsync(request.Email, request.IsCoach);
            return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        try
        {
            var result = await authService.VerifyOtpAsync(request.Email, request.Code, request.IsCoach);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }

    [HttpPost("token/refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await authService.RefreshTokenAsync(request.RefreshToken);
            return Ok(new { success = true, data = result, error = (string?)null, statusCode = 200 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, data = (object?)null, error = ex.Message, statusCode = 400 });
        }
    }

    /// <summary>
    /// Revokes the caller's refresh token and, if a device token is supplied, removes that
    /// device's push-notification registration. Requires a valid JWT — any role (coach or
    /// member) may call this.
    /// </summary>
    /// <remarks>
    /// Always returns 200, even if the refresh token is unknown, already revoked, expired, or
    /// belongs to someone else — the response never reveals whether it existed. The caller's
    /// access token (JWT) is NOT revoked by this call: JWTs cannot be revoked server-side, so it
    /// stays valid until it expires naturally. Logout only stops renewal.
    /// </remarks>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await authService.LogoutAsync(userId, request, ct);
        return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
    }
}
