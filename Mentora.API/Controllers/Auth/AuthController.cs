using Mentora.Core.DTOs.Auth;
using Mentora.Core.Interfaces;
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
            await authService.RequestOtpAsync(request.Email);
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
            var result = await authService.VerifyOtpAsync(request.Email, request.Code);
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
}
