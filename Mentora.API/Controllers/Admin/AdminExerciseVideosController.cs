using Mentora.API.Authorization;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mentora.API.Controllers.Admin;

/// <summary>
/// Lot 12.3a — MUX video of a Mentora exercise (AdminBearer token only). The browser uploads the file straight
/// to MUX with the signed URL returned by <c>video-uploads</c>; the file never goes through this API.
/// Scope: Mentora exercises (no coach owner), inactive included; a coach-owned or unknown id is a 404.
/// No body on any route. Errors: 401, 404, 502 MUX_UNAVAILABLE, 503 MUX_NOT_CONFIGURED.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
[ApiExplorerSettings(GroupName = AdminAuthDefaults.SwaggerDoc)]
[Tags("Admin: Exercise videos")]
public class AdminExerciseVideosController(IAdminExerciseVideoService videoService) : ControllerBase
{
    /// <summary>
    /// Creates a MUX direct upload for the exercise and sets its video to UPLOADING (replacing any previous
    /// video: the previous MUX asset is deleted, a previous pending upload cancelled, after the commit).
    /// <c>uploadUrl</c> accepts one PUT of the file until <c>expiresAt</c>.
    /// </summary>
    /// <param name="exerciseId">Exercise identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("exercises/{exerciseId:guid}/video-uploads")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateUpload(Guid exerciseId, CancellationToken ct)
    {
        var result = await videoService.CreateUploadAsync(exerciseId, GetAdminUserId(), ct);
        return StatusCode(StatusCodes.Status201Created, Envelope(result, 201));
    }

    /// <summary>
    /// Reads MUX and updates the video (UPLOADING to WAITING, READY or ERRORED; WAITING to READY or ERRORED);
    /// READY and ERRORED are returned as they are, without calling MUX. The admin polls it while a video is
    /// processing. 404 when the exercise has no video.
    /// </summary>
    /// <param name="exerciseId">Exercise identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("exercises/{exerciseId:guid}/video/sync")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Sync(Guid exerciseId, CancellationToken ct)
    {
        var result = await videoService.SyncAsync(exerciseId, ct);
        return Ok(Envelope(result, 200));
    }

    /// <summary>
    /// Removes the video of the exercise, then deletes the MUX asset or cancels the pending upload (best
    /// effort). 200 with data null, like <c>DELETE /api/v1/devices</c>. 404 when the exercise has no video.
    /// </summary>
    /// <param name="exerciseId">Exercise identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("exercises/{exerciseId:guid}/video")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Remove(Guid exerciseId, CancellationToken ct)
    {
        await videoService.RemoveAsync(exerciseId, GetAdminUserId(), ct);
        return Ok(new { success = true, data = (object?)null, error = (string?)null, statusCode = 200 });
    }

    /// <summary>The admin user id of the AdminBearer token; a missing or malformed claim is a 401, never a default.</summary>
    private Guid GetAdminUserId()
    {
        if (!Guid.TryParse(User.FindFirst(AdminAuthDefaults.AdminIdClaim)?.Value, out var adminUserId))
            throw new UnauthorizedException("Admin account not found or disabled.");
        return adminUserId;
    }

    private static object Envelope(object data, int statusCode) =>
        new { success = true, data, error = (string?)null, statusCode };
}
