using Mentora.Core.DTOs.AdminExercise;
using Mentora.Core.DTOs.Exercise;
using Mentora.Core.Entities;
using Mentora.Core.Enums;

namespace Mentora.Infrastructure.Services.Mux;

/// <summary>Lot 12.3a — the two views of an exercise video row.</summary>
public static class ExerciseVideoMapping
{
    /// <summary>
    /// Apps (member and coach routes, program trees), decision 2A: the video only when READY with a playback
    /// id, otherwise null. The DTO shape is unchanged.
    /// </summary>
    public static ExerciseVideoResponse? ForApps(ExerciseVideo? v) =>
        v is { ExerciseVideoStatus: ExerciseVideoStatus.Ready, ExerciseVideoMuxPlaybackId: not null }
            ? new ExerciseVideoResponse(
                v.ExerciseVideoMuxPlaybackId,
                EnumMappings.ExerciseVideoStatusMapping.ToWire(v.ExerciseVideoStatus),
                v.ExerciseVideoDurationSeconds,
                v.ExerciseVideoAspectRatio)
            : null;

    /// <summary>Admin back-office: every status, with the error and the upload expiry.</summary>
    public static AdminExerciseVideoResponse? ForAdmin(ExerciseVideo? v) =>
        v is null ? null : ForAdminRequired(v);

    public static AdminExerciseVideoResponse ForAdminRequired(ExerciseVideo v) => new(
        EnumMappings.ExerciseVideoStatusMapping.ToWire(v.ExerciseVideoStatus),
        v.ExerciseVideoMuxPlaybackId,
        v.ExerciseVideoDurationSeconds,
        v.ExerciseVideoAspectRatio,
        v.ExerciseVideoError,
        v.ExerciseVideoUploadExpiresDate,
        v.ExerciseVideoUpdatedDate);
}
