using System.Text.Json;
using Mentora.Core.DTOs.AdminExercise;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services.Mux;

/// <summary>
/// Lot 12.3a — admin side of the MUX videos (decision 12.I): the file goes from the browser straight to MUX
/// through a signed direct-upload URL; this service creates the upload, follows it (sync) and removes it.
/// Scope: Mentora exercises (EXERCISE_COACH_ID NULL), inactive included. One video row per exercise: a new
/// upload reuses the row (its id is the MUX passthrough) and the previous MUX asset or upload is cleaned up
/// after the commit, best effort.
/// </summary>
public sealed class AdminExerciseVideoService(
    MentoraDbContext db,
    IMuxClient mux,
    IOptions<MuxOptions> options,
    ILogger<AdminExerciseVideoService> logger) : IAdminExerciseVideoService
{
    // ── Upload creation ────────────────────────────────────────────────────────

    public async Task<AdminExerciseVideoUploadResponse> CreateUploadAsync(
        Guid exerciseId, Guid adminUserId, CancellationToken ct)
    {
        EnsureConfigured();
        await EnsureAdminAsync(adminUserId, ct);

        var now = DateTime.UtcNow;
        MuxDirectUpload upload;
        ExerciseVideo video;
        string? previousAssetId;
        string? previousUploadId;
        ExerciseVideoStatus? previousStatus;

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            // Serialises two uploads of the same exercise: the second one waits here, then replaces the first.
            if (!await MuxVideoRules.LockExerciseAsync(db, exerciseId, ct))
                throw new NotFoundException($"Exercise {exerciseId} not found.");

            var exercise = await db.Exercises
                .AsNoTracking()
                .Where(e => e.ExerciseId == exerciseId)
                .Select(e => new { e.ExerciseName, e.ExerciseSourceCode })
                .SingleAsync(ct);

            var existing = await db.ExerciseVideos.FirstOrDefaultAsync(v => v.ExerciseVideoExerciseId == exerciseId, ct);
            var videoId = existing?.ExerciseVideoId ?? Guid.NewGuid();

            upload = await mux.CreateDirectUploadAsync(
                videoId.ToString(),
                exercise.ExerciseName,
                exercise.ExerciseSourceCode ?? exerciseId.ToString(),
                ct);

            try
            {
                previousAssetId  = existing?.ExerciseVideoMuxAssetId;
                previousUploadId = existing?.ExerciseVideoMuxUploadId;
                previousStatus   = existing?.ExerciseVideoStatus;

                if (existing is null)
                {
                    existing = new ExerciseVideo
                    {
                        ExerciseVideoId             = videoId,
                        ExerciseVideoExerciseId     = exerciseId,
                        ExerciseVideoPlaybackPolicy = VideoPlaybackPolicy.Public,
                        ExerciseVideoCreatedDate    = now,
                    };
                    db.ExerciseVideos.Add(existing);
                }
                video = existing;

                video.ExerciseVideoMuxUploadId       = upload.Id;
                video.ExerciseVideoMuxAssetId        = null;
                video.ExerciseVideoMuxPlaybackId     = null;
                video.ExerciseVideoDurationSeconds   = null;
                video.ExerciseVideoAspectRatio       = null;
                video.ExerciseVideoError             = null;
                video.ExerciseVideoStatus            = ExerciseVideoStatus.Uploading;
                video.ExerciseVideoUploadExpiresDate = now.AddSeconds(upload.TimeoutSeconds ?? options.Value.UploadTimeoutSeconds);
                video.ExerciseVideoUpdatedDate       = now;

                db.AdminAuditLogs.Add(NewAudit(adminUserId, AdminAuditAction.ExerciseVideoUploadCreated, exerciseId,
                    new Dictionary<string, object?>
                    {
                        ["uploadId"]         = upload.Id,
                        ["previousAssetId"]  = previousAssetId,
                        ["previousUploadId"] = previousUploadId,
                    }, now));

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                // The new MUX upload is not referenced by any row: cancel it (else it expires on its own).
                await BestEffortAsync("create upload rollback", videoId, assetId: null, uploadIdToCancel: upload.Id);
                throw;
            }
        }

        logger.LogInformation("[MUX] upload created video={VideoId} exercise={ExerciseId} uploadId={UploadId}",
            video.ExerciseVideoId, exerciseId, upload.Id);

        await BestEffortAsync("replace", video.ExerciseVideoId, previousAssetId,
            previousStatus == ExerciseVideoStatus.Uploading ? previousUploadId : null);

        return new AdminExerciseVideoUploadResponse(
            upload.Id,
            upload.Url,
            video.ExerciseVideoUploadExpiresDate!.Value,
            ExerciseVideoMapping.ForAdminRequired(video));
    }

    // ── Sync ───────────────────────────────────────────────────────────────────

    public async Task<AdminExerciseVideoSyncResponse> SyncAsync(Guid exerciseId, CancellationToken ct)
    {
        EnsureConfigured();
        await EnsureMentoraExerciseAsync(exerciseId, ct);

        var snapshot = await db.ExerciseVideos.AsNoTracking()
            .FirstOrDefaultAsync(v => v.ExerciseVideoExerciseId == exerciseId, ct)
            ?? throw new NotFoundException($"Exercise {exerciseId} has no video.");

        // Read MUX outside the lock; the facts are applied under the lock with the same guards as the webhook.
        var facts = new List<Func<ExerciseVideo, DateTime, MuxRuleOutcome>>();

        if (snapshot.ExerciseVideoStatus == ExerciseVideoStatus.Uploading && snapshot.ExerciseVideoMuxUploadId is { } uploadId)
        {
            var upload = await mux.GetUploadAsync(uploadId, ct);
            if (upload is null)
                facts.Add((v, now) => MuxVideoRules.OnUploadFailed(v, uploadId, MuxVideoRules.UploadNotFound, null, now));
            else if (upload.Status == "asset_created" && upload.AssetId is { } assetId)
            {
                facts.Add((v, now) => MuxVideoRules.OnUploadAssetCreated(v, uploadId, assetId, now));
                AddAssetFacts(facts, assetId, await mux.GetAssetAsync(assetId, ct));
            }
            else if (upload.Status == "errored")
                facts.Add((v, now) => MuxVideoRules.OnUploadFailed(v, uploadId, MuxVideoRules.UploadErrored, upload.ErrorMessage, now));
            else if (upload.Status == "cancelled")
                facts.Add((v, now) => MuxVideoRules.OnUploadFailed(v, uploadId, MuxVideoRules.UploadCancelled, null, now));
            else if (upload.Status == "timed_out")
                facts.Add((v, now) => MuxVideoRules.OnUploadFailed(v, uploadId, MuxVideoRules.UploadTimedOut, null, now));
            // waiting (even past the expiry): unchanged.
        }
        else if (snapshot.ExerciseVideoStatus == ExerciseVideoStatus.Waiting && snapshot.ExerciseVideoMuxAssetId is { } assetId)
        {
            AddAssetFacts(facts, assetId, await mux.GetAssetAsync(assetId, ct));
        }
        // READY or ERRORED: no MUX call.

        if (facts.Count == 0)
            return new AdminExerciseVideoSyncResponse(ExerciseVideoMapping.ForAdminRequired(snapshot));

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await MuxVideoRules.LockExerciseAsync(db, exerciseId, ct))
            throw new NotFoundException($"Exercise {exerciseId} not found.");
        var video = await db.ExerciseVideos.FirstOrDefaultAsync(v => v.ExerciseVideoExerciseId == exerciseId, ct)
                    ?? throw new NotFoundException($"Exercise {exerciseId} has no video.");

        var now = DateTime.UtcNow;
        foreach (var fact in facts)
        {
            var outcome = fact(video, now);
            logger.LogInformation("[MUX] sync result={Result}", outcome.Result);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new AdminExerciseVideoSyncResponse(ExerciseVideoMapping.ForAdminRequired(video));
    }

    private static void AddAssetFacts(
        List<Func<ExerciseVideo, DateTime, MuxRuleOutcome>> facts, string assetId, MuxAsset? asset)
    {
        if (asset is null)
            facts.Add((v, now) => MuxVideoRules.OnAssetMissing(v, assetId, now));
        else if (asset.Status == "ready")
            facts.Add((v, now) => MuxVideoRules.OnAssetReady(v, asset, now));
        else if (asset.Status == "errored")
            facts.Add((v, now) => MuxVideoRules.OnAssetErrored(v, asset, now));
        // preparing: unchanged.
    }

    // ── Removal ────────────────────────────────────────────────────────────────

    public async Task RemoveAsync(Guid exerciseId, Guid adminUserId, CancellationToken ct)
    {
        EnsureConfigured();
        await EnsureAdminAsync(adminUserId, ct);

        Guid videoId;
        string? assetId;
        string? uploadId;
        ExerciseVideoStatus status;

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            if (!await MuxVideoRules.LockExerciseAsync(db, exerciseId, ct))
                throw new NotFoundException($"Exercise {exerciseId} not found.");

            var video = await db.ExerciseVideos.FirstOrDefaultAsync(v => v.ExerciseVideoExerciseId == exerciseId, ct)
                        ?? throw new NotFoundException($"Exercise {exerciseId} has no video.");

            videoId  = video.ExerciseVideoId;
            assetId  = video.ExerciseVideoMuxAssetId;
            uploadId = video.ExerciseVideoMuxUploadId;
            status   = video.ExerciseVideoStatus;

            db.ExerciseVideos.Remove(video);
            db.AdminAuditLogs.Add(NewAudit(adminUserId, AdminAuditAction.ExerciseVideoRemoved, exerciseId,
                new Dictionary<string, object?> { ["assetId"] = assetId, ["uploadId"] = uploadId },
                DateTime.UtcNow));

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        logger.LogInformation("[MUX] video removed video={VideoId} exercise={ExerciseId}", videoId, exerciseId);
        await BestEffortAsync("remove", videoId, assetId,
            status == ExerciseVideoStatus.Uploading ? uploadId : null);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Deletes an asset and/or cancels an upload at MUX; never throws (the database is already committed).</summary>
    private async Task BestEffortAsync(string context, Guid videoId, string? assetId, string? uploadIdToCancel)
    {
        if (assetId is not null)
        {
            try
            {
                var deleted = await mux.DeleteAssetAsync(assetId, CancellationToken.None);
                logger.LogInformation("[MUX] {Context} video={VideoId} asset {AssetId} {Outcome}",
                    context, videoId, assetId, deleted ? "deleted" : "already gone");
            }
            catch (Exception ex)
            {
                logger.LogWarning("[MUX] {Context} video={VideoId} asset {AssetId} not deleted ({Error})",
                    context, videoId, assetId, ex.GetType().Name);
            }
        }

        if (uploadIdToCancel is not null)
        {
            try
            {
                var cancelled = await mux.CancelUploadAsync(uploadIdToCancel, CancellationToken.None);
                logger.LogInformation("[MUX] {Context} video={VideoId} upload {UploadId} {Outcome}",
                    context, videoId, uploadIdToCancel, cancelled ? "cancelled" : "already gone");
            }
            catch (Exception ex)
            {
                logger.LogWarning("[MUX] {Context} video={VideoId} upload {UploadId} not cancelled ({Error})",
                    context, videoId, uploadIdToCancel, ex.GetType().Name);
            }
        }
    }

    private void EnsureConfigured()
    {
        if (!options.Value.IsConfigured)
            throw new MuxNotConfiguredException();
    }

    private async Task EnsureMentoraExerciseAsync(Guid exerciseId, CancellationToken ct)
    {
        var exists = await db.Exercises.AnyAsync(e => e.ExerciseId == exerciseId && e.ExerciseCoachId == null, ct);
        if (!exists)
            throw new NotFoundException($"Exercise {exerciseId} not found.");
    }

    /// <summary>Same rule as the other admin writes: the token of a disabled admin stays valid until it expires.</summary>
    private async Task EnsureAdminAsync(Guid adminUserId, CancellationToken ct)
    {
        var ok = await db.AdminUsers.AnyAsync(a => a.AdminUserId == adminUserId && a.AdminUserIsEnabled, ct);
        if (!ok)
            throw new UnauthorizedException("Admin account not found or disabled.");
    }

    private static AdminAuditLog NewAudit(
        Guid adminUserId, AdminAuditAction action, Guid exerciseId, object changes, DateTime now) => new()
    {
        AdminAuditLogId          = Guid.NewGuid(),
        AdminAuditLogAdminUserId = adminUserId,
        AdminAuditLogAction      = action,
        AdminAuditLogEntityType  = AdminAuditEntityType.Exercise,
        AdminAuditLogEntityId    = exerciseId,
        AdminAuditLogChanges     = JsonSerializer.Serialize(changes),
        AdminAuditLogCreatedDate = now,
    };
}
