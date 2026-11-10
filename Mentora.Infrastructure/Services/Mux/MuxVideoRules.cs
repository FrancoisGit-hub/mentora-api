using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services.Mux;

/// <summary>Result of applying one MUX fact to a video row. Applied false = the fact did not match the row.</summary>
public sealed record MuxRuleOutcome(bool Applied, string Result);

/// <summary>
/// Lot 12.3a — state rules of EXERCISE_VIDEOS, shared by the webhook and the admin sync route. Never move
/// backwards (a READY row never goes back to WAITING); a fact about an asset other than the row's current
/// asset is ignored (the video was replaced); a fact about an upload other than the row's current upload is
/// ignored. The caller holds the exercise row lock (<see cref="LockExerciseAsync"/>) and saves.
/// </summary>
public static class MuxVideoRules
{
    public const int MaxErrorLength = 500;
    public const int MaxAspectRatioLength = 10;

    public const string UploadErrored = "UPLOAD_ERRORED";
    public const string UploadCancelled = "UPLOAD_CANCELLED";
    public const string UploadTimedOut = "UPLOAD_TIMED_OUT";
    public const string UploadNotFound = "UPLOAD_NOT_FOUND";
    public const string AssetErrored = "ASSET_ERRORED";
    public const string AssetNotFound = "ASSET_NOT_FOUND";

    /// <summary>
    /// SELECT ... FOR UPDATE on the exercise row: every writer of an exercise video row (upload creation,
    /// removal, sync, webhook) takes it first, so they run one after the other. Scope: Mentora exercises only.
    /// Returns false when no such exercise exists.
    /// </summary>
    public static async Task<bool> LockExerciseAsync(MentoraDbContext db, Guid exerciseId, CancellationToken ct)
    {
        var ids = await db.Database
            .SqlQuery<Guid>($"""
                SELECT "EXERCISE_ID" AS "Value" FROM "EXERCISES"
                WHERE "EXERCISE_ID" = {exerciseId} AND "EXERCISE_COACH_ID" IS NULL
                FOR UPDATE
                """)
            .ToListAsync(ct);
        return ids.Count == 1;
    }

    /// <summary>video.upload.asset_created, or a sync that finds the upload in asset_created.</summary>
    public static MuxRuleOutcome OnUploadAssetCreated(ExerciseVideo v, string uploadId, string assetId, DateTime now)
    {
        if (v.ExerciseVideoMuxUploadId != uploadId)
            return new(false, $"video {v.ExerciseVideoId} upload replaced");
        if (v.ExerciseVideoStatus != ExerciseVideoStatus.Uploading)
        {
            // Already past this step (asset created then ready or errored): consistent, nothing to do.
            return v.ExerciseVideoMuxAssetId == assetId
                ? new(true, $"video {v.ExerciseVideoId} {Wire(v)} (unchanged)")
                : new(false, $"video {v.ExerciseVideoId} not uploading");
        }

        v.ExerciseVideoMuxAssetId = assetId;
        v.ExerciseVideoStatus = ExerciseVideoStatus.Waiting;
        v.ExerciseVideoError = null;
        v.ExerciseVideoUpdatedDate = now;
        return new(true, $"video {v.ExerciseVideoId} WAITING");
    }

    /// <summary>video.asset.ready (or a sync that reads a ready asset).</summary>
    public static MuxRuleOutcome OnAssetReady(ExerciseVideo v, MuxAsset asset, DateTime now)
    {
        if (!IsCurrentAsset(v, asset, out var ignored))
            return ignored!;

        var playbackId = asset.PlaybackIds
            .FirstOrDefault(p => string.Equals(p.Policy, "public", StringComparison.OrdinalIgnoreCase))?.Id;
        var aspectRatio = MuxJson.Truncate(asset.AspectRatio, MaxAspectRatioLength);

        var unchanged = v.ExerciseVideoStatus == ExerciseVideoStatus.Ready
                        && v.ExerciseVideoMuxAssetId == asset.Id
                        && v.ExerciseVideoMuxPlaybackId == playbackId
                        && v.ExerciseVideoDurationSeconds == asset.DurationSeconds
                        && v.ExerciseVideoAspectRatio == aspectRatio
                        && v.ExerciseVideoError is null;
        if (unchanged)
            return new(true, $"video {v.ExerciseVideoId} READY (unchanged)");

        v.ExerciseVideoMuxAssetId = asset.Id;
        v.ExerciseVideoStatus = ExerciseVideoStatus.Ready;
        v.ExerciseVideoMuxPlaybackId = playbackId;
        v.ExerciseVideoDurationSeconds = asset.DurationSeconds;
        v.ExerciseVideoAspectRatio = aspectRatio;
        v.ExerciseVideoError = null;
        v.ExerciseVideoUpdatedDate = now;
        return new(true, $"video {v.ExerciseVideoId} READY");
    }

    /// <summary>video.asset.errored (or a sync that reads an errored asset).</summary>
    public static MuxRuleOutcome OnAssetErrored(ExerciseVideo v, MuxAsset asset, DateTime now)
    {
        if (!IsCurrentAsset(v, asset, out var ignored))
            return ignored!;

        var message = asset.ErrorMessages.Count > 0 ? string.Join("; ", asset.ErrorMessages) : "no message";
        return SetErrored(v, AssetErrored, message, now, asset.Id);
    }

    /// <summary>video.upload.errored / cancelled, or a sync that finds the upload errored, cancelled, timed out or gone.</summary>
    public static MuxRuleOutcome OnUploadFailed(ExerciseVideo v, string uploadId, string code, string? message, DateTime now)
    {
        if (v.ExerciseVideoMuxUploadId != uploadId)
            return new(false, $"video {v.ExerciseVideoId} upload replaced");
        if (v.ExerciseVideoStatus != ExerciseVideoStatus.Uploading)
            return new(false, $"video {v.ExerciseVideoId} not uploading");
        return SetErrored(v, code, message ?? DefaultMessage(code), now, assetId: null);
    }

    /// <summary>Sync only: MUX no longer knows the row's asset.</summary>
    public static MuxRuleOutcome OnAssetMissing(ExerciseVideo v, string assetId, DateTime now)
    {
        if (v.ExerciseVideoMuxAssetId != assetId)
            return new(false, $"video {v.ExerciseVideoId} asset replaced");
        return SetErrored(v, AssetNotFound, "MUX no longer knows this asset", now, assetId);
    }

    private static MuxRuleOutcome SetErrored(ExerciseVideo v, string code, string message, DateTime now, string? assetId)
    {
        var error = MuxJson.Truncate(code + ": " + message, MaxErrorLength);
        if (v.ExerciseVideoStatus == ExerciseVideoStatus.Errored && v.ExerciseVideoError == error
            && (assetId is null || v.ExerciseVideoMuxAssetId == assetId))
            return new(true, $"video {v.ExerciseVideoId} ERRORED (unchanged)");

        if (assetId is not null)
            v.ExerciseVideoMuxAssetId = assetId;
        v.ExerciseVideoStatus = ExerciseVideoStatus.Errored;
        v.ExerciseVideoError = error;
        v.ExerciseVideoUpdatedDate = now;
        return new(true, $"video {v.ExerciseVideoId} ERRORED {code}");
    }

    /// <summary>
    /// The asset belongs to the row when it is the row's asset, or, while the row has no asset yet, when it
    /// comes from the row's current upload. The passthrough (the row id) survives replacements, so it is not
    /// enough on its own.
    /// </summary>
    private static bool IsCurrentAsset(ExerciseVideo v, MuxAsset asset, out MuxRuleOutcome? ignored)
    {
        ignored = null;
        if (v.ExerciseVideoMuxAssetId is not null)
        {
            if (v.ExerciseVideoMuxAssetId == asset.Id) return true;
            ignored = new(false, $"video {v.ExerciseVideoId} asset replaced");
            return false;
        }
        if (asset.UploadId is not null && asset.UploadId == v.ExerciseVideoMuxUploadId) return true;
        ignored = new(false, $"video {v.ExerciseVideoId} asset of another upload");
        return false;
    }

    private static string DefaultMessage(string code)
    {
        if (code == UploadCancelled) return "upload cancelled";
        if (code == UploadTimedOut) return "upload URL expired before the file was received";
        if (code == UploadNotFound) return "MUX no longer knows this upload";
        return "no message";
    }

    private static string Wire(ExerciseVideo v) => EnumMappings.ExerciseVideoStatusMapping.ToWire(v.ExerciseVideoStatus);
}
