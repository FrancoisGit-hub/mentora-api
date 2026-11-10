using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

/// <summary>
/// Mux video attached to an exercise. One video per exercise for now
/// (unique EXERCISE_VIDEO_EXERCISE_ID; a later lot lifts it).
/// Lot 12.3a: a row starts as UPLOADING with a MUX upload id and no asset id; at least one of the
/// two ids is always set (CK_EXERCISE_VIDEOS_MUX_REF). The row id is the MUX passthrough.
/// </summary>
public class ExerciseVideo
{
    public Guid ExerciseVideoId { get; set; }

    public Guid ExerciseVideoExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public string? ExerciseVideoMuxAssetId { get; set; }
    public string? ExerciseVideoMuxPlaybackId { get; set; }

    /// <summary>Lot 12.3a — MUX direct upload id of the current (latest) upload.</summary>
    public string? ExerciseVideoMuxUploadId { get; set; }

    /// <summary>Lot 12.3a — when the signed upload URL stops accepting the file.</summary>
    public DateTime? ExerciseVideoUploadExpiresDate { get; set; }

    /// <summary>Lot 12.3a — short code, ": ", MUX message (max 500), set when ERRORED.</summary>
    public string? ExerciseVideoError { get; set; }

    public VideoPlaybackPolicy ExerciseVideoPlaybackPolicy { get; set; } = VideoPlaybackPolicy.Public;
    public ExerciseVideoStatus ExerciseVideoStatus { get; set; } = ExerciseVideoStatus.Waiting;

    public decimal? ExerciseVideoDurationSeconds { get; set; }
    public string? ExerciseVideoAspectRatio { get; set; }

    public DateTime ExerciseVideoCreatedDate { get; set; }
    public DateTime ExerciseVideoUpdatedDate { get; set; }
}
