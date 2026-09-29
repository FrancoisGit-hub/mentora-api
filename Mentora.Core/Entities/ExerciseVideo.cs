using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

/// <summary>
/// Mux video attached to an exercise. One video per exercise for now
/// (unique EXERCISE_VIDEO_EXERCISE_ID; a later lot lifts it).
/// </summary>
public class ExerciseVideo
{
    public Guid ExerciseVideoId { get; set; }

    public Guid ExerciseVideoExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public string ExerciseVideoMuxAssetId { get; set; } = null!;
    public string? ExerciseVideoMuxPlaybackId { get; set; }

    public VideoPlaybackPolicy ExerciseVideoPlaybackPolicy { get; set; } = VideoPlaybackPolicy.Public;
    public ExerciseVideoStatus ExerciseVideoStatus { get; set; } = ExerciseVideoStatus.Waiting;

    public decimal? ExerciseVideoDurationSeconds { get; set; }
    public string? ExerciseVideoAspectRatio { get; set; }

    public DateTime ExerciseVideoCreatedDate { get; set; }
    public DateTime ExerciseVideoUpdatedDate { get; set; }
}
