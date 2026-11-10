namespace Mentora.Core.DTOs.AdminExercise;

// Lot 12.3a — MUX video of a Mentora exercise, admin back-office only. The apps keep ExerciseVideoResponse
// (READY with a playback id only).

/// <summary>Video row of an exercise as the admin sees it, whatever its status.</summary>
/// <param name="Status">UPLOADING, WAITING, READY or ERRORED.</param>
/// <param name="PlaybackId">MUX public playback id (READY only, in practice).</param>
/// <param name="DurationSeconds">Duration rounded to 2 decimals.</param>
/// <param name="AspectRatio">MUX aspect ratio, for example "16:9".</param>
/// <param name="Error">ERRORED only: short code, ": ", MUX message.</param>
/// <param name="UploadExpiresAt">When the signed upload URL of the current upload expires.</param>
/// <param name="UpdatedDate">Last change of the row.</param>
public record AdminExerciseVideoResponse(
    string Status,
    string? PlaybackId,
    decimal? DurationSeconds,
    string? AspectRatio,
    string? Error,
    DateTime? UploadExpiresAt,
    DateTime UpdatedDate);

/// <summary>
/// Answer of POST /api/v1/admin/exercises/{id}/video-uploads. <paramref name="UploadUrl"/> accepts one PUT
/// of the file until <paramref name="ExpiresAt"/>: it is a credential, never logged.
/// </summary>
public record AdminExerciseVideoUploadResponse(
    string UploadId,
    string UploadUrl,
    DateTime ExpiresAt,
    AdminExerciseVideoResponse Video);

/// <summary>Answer of POST /api/v1/admin/exercises/{id}/video/sync.</summary>
public record AdminExerciseVideoSyncResponse(AdminExerciseVideoResponse Video);
