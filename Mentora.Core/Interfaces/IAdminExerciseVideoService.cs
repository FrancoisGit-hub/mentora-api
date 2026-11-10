using Mentora.Core.DTOs.AdminExercise;

namespace Mentora.Core.Interfaces;

/// <summary>
/// Lot 12.3a — MUX video of a Mentora exercise (EXERCISE_COACH_ID NULL, inactive included; anything else is
/// a 404). Every method throws MuxNotConfiguredException (503) when MUX is not configured and
/// MuxUnavailableException (502) when MUX fails.
/// </summary>
public interface IAdminExerciseVideoService
{
    /// <summary>Creates a MUX direct upload and points the exercise video row at it (status UPLOADING).</summary>
    Task<AdminExerciseVideoUploadResponse> CreateUploadAsync(Guid exerciseId, Guid adminUserId, CancellationToken ct);

    /// <summary>Reads MUX and applies the webhook state rules. NotFoundException when the exercise has no video row.</summary>
    Task<AdminExerciseVideoSyncResponse> SyncAsync(Guid exerciseId, CancellationToken ct);

    /// <summary>Deletes the video row, then the MUX asset or upload (best effort). NotFoundException when no row.</summary>
    Task RemoveAsync(Guid exerciseId, Guid adminUserId, CancellationToken ct);
}
