using System.Security.Claims;
using Mentora.Core.DTOs.Device;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Auth;

/// <summary>Push-notification device registration, shared by both the coach and member apps.</summary>
[ApiController]
[Route("api/v1/devices")]
[Authorize]
[ApiExplorerSettings(GroupName = "mobile")]
[Tags("Devices")]
public class UserDevicesController(IUserDeviceService userDeviceService) : ControllerBase
{
    /// <summary>
    /// Registers (or re-registers) a push-notification device token for the authenticated user.
    /// Upsert keyed on the token itself: unseen tokens are inserted, tokens already owned by
    /// this user just get a fresh LastSeenDate, and tokens owned by a DIFFERENT user are
    /// reassigned to the caller — a device that changes hands must stop notifying its previous
    /// owner, never keep a duplicate row.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await userDeviceService.RegisterAsync(userId, request, ct);
        return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
    }

    /// <summary>
    /// Deletes a device registration for the authenticated user. Always returns 200, whether or
    /// not the token existed or belonged to someone else — deletion only ever happens when the
    /// token belongs to the caller.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] DeleteDeviceRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await userDeviceService.DeleteAsync(userId, request.Token, ct);
        return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
    }
}
