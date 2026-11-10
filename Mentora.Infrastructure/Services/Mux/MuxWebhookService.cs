using System.Text;
using System.Text.Json;
using Mentora.Core.DTOs.Mux;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Mentora.Infrastructure.Services.Mux;

/// <summary>
/// Lot 12.3a — MUX webhook. Order: signature over the raw bytes, then JSON, then one transaction that records
/// the event (unique MUX event id = idempotence) and applies <see cref="MuxVideoRules"/>. Never calls MUX:
/// api-demo also receives the events of uploads made from a local API on the same MUX environment, and those
/// simply match no row (IGNORED).
/// </summary>
public sealed class MuxWebhookService(
    MentoraDbContext db,
    IOptions<MuxOptions> options,
    ILogger<MuxWebhookService> logger) : IMuxWebhookService
{
    private const int MaxMuxIdLength = 100;
    private const int MaxTypeLength = 80;
    private const int MaxObjectIdLength = 100;
    private const int MaxResultLength = 200;

    private const string UniqueEventIndex = "UX_MUX_WEBHOOK_EVENTS_MUX_ID";

    public async Task<MuxWebhookResult> HandleAsync(byte[] rawBody, string? signatureHeader, CancellationToken ct)
    {
        var opts = options.Value;
        var reason = MuxWebhookSignature.Verify(
            signatureHeader, rawBody, opts.WebhookSecret, opts.WebhookToleranceSeconds, DateTimeOffset.UtcNow);
        if (reason is not null)
        {
            logger.LogWarning("[MUX] webhook rejected reason={Reason}", reason);
            return new MuxWebhookResult(MuxWebhookOutcome.Rejected, reason, null, null);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            logger.LogWarning("[MUX] webhook body is not JSON");
            return new MuxWebhookResult(MuxWebhookOutcome.InvalidPayload, null, null, "Invalid JSON payload.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var eventId = MuxJson.Str(root, "id");
            var type = MuxJson.Str(root, "type");
            if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(type) || eventId.Length > MaxMuxIdLength)
            {
                logger.LogWarning("[MUX] webhook payload without a usable id or type");
                return new MuxWebhookResult(MuxWebhookOutcome.InvalidPayload, null, null, "Invalid MUX event payload.");
            }

            type = MuxJson.Truncate(type, MaxTypeLength)!;

            if (await db.MuxWebhookEvents.AnyAsync(e => e.MuxWebhookEventMuxId == eventId, ct))
                return Duplicate(type);

            var data = root.TryGetProperty("data", out var d) ? d : default;
            var objectId = MuxJson.Obj(root, "object") is { } obj ? MuxJson.Str(obj, "id") : null;

            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var outcome = await ApplyAsync(type, data, ct);

                db.MuxWebhookEvents.Add(new MuxWebhookEvent
                {
                    MuxWebhookEventId          = Guid.NewGuid(),
                    MuxWebhookEventMuxId       = eventId,
                    MuxWebhookEventType        = type,
                    MuxWebhookEventObjectId    = MuxJson.Truncate(objectId, MaxObjectIdLength),
                    MuxWebhookEventStatus      = outcome.Applied ? MuxWebhookEventStatus.Processed : MuxWebhookEventStatus.Ignored,
                    MuxWebhookEventResult      = MuxJson.Truncate(outcome.Result, MaxResultLength),
                    MuxWebhookEventPayload     = Encoding.UTF8.GetString(rawBody),
                    MuxWebhookEventCreatedDate = DateTime.UtcNow,
                });

                // One SaveChanges: the event row and the video change are written together or not at all.
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                logger.LogInformation("[MUX] event type={Type} result={Result}", type, outcome.Result);
                return new MuxWebhookResult(
                    outcome.Applied ? MuxWebhookOutcome.Processed : MuxWebhookOutcome.Ignored, null, type, outcome.Result);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                               {
                                                   SqlState: PostgresErrorCodes.UniqueViolation,
                                                   ConstraintName: UniqueEventIndex
                                               })
            {
                // Same event delivered twice at the same time: the other request recorded it first.
                db.ChangeTracker.Clear();
                return Duplicate(type);
            }
        }
    }

    private MuxWebhookResult Duplicate(string type)
    {
        logger.LogInformation("[MUX] event type={Type} result=duplicate", type);
        return new MuxWebhookResult(MuxWebhookOutcome.Duplicate, null, type, "duplicate");
    }

    private async Task<MuxRuleOutcome> ApplyAsync(string type, JsonElement data, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        if (type == "video.upload.asset_created")
        {
            var upload = MuxJson.ParseUpload(data);
            if (upload?.AssetId is null) return new(false, "payload without upload id or asset id");
            var video = await FindAndLockAsync(upload.AssetId, upload.Id, upload.Passthrough, ct);
            return video is null
                ? new(false, "no matching video")
                : MuxVideoRules.OnUploadAssetCreated(video, upload.Id, upload.AssetId, now);
        }

        if (type == "video.upload.errored" || type == "video.upload.cancelled")
        {
            var upload = MuxJson.ParseUpload(data);
            if (upload is null) return new(false, "payload without upload id");
            var video = await FindAndLockAsync(null, upload.Id, upload.Passthrough, ct);
            if (video is null) return new(false, "no matching video");
            return type == "video.upload.errored"
                ? MuxVideoRules.OnUploadFailed(video, upload.Id, MuxVideoRules.UploadErrored, upload.ErrorMessage, now)
                : MuxVideoRules.OnUploadFailed(video, upload.Id, MuxVideoRules.UploadCancelled, null, now);
        }

        if (type == "video.asset.ready" || type == "video.asset.errored")
        {
            var asset = MuxJson.ParseAsset(data);
            if (asset is null) return new(false, "payload without asset id");
            var video = await FindAndLockAsync(asset.Id, asset.UploadId, asset.Passthrough, ct);
            if (video is null) return new(false, "no matching video");
            return type == "video.asset.ready"
                ? MuxVideoRules.OnAssetReady(video, asset, now)
                : MuxVideoRules.OnAssetErrored(video, asset, now);
        }

        if (type == "video.asset.deleted")
        {
            var assetId = MuxJson.Str(data, "id");
            if (string.IsNullOrEmpty(assetId)) return new(false, "payload without asset id");
            var video = await FindAndLockAsync(assetId, null, null, ct);
            if (video is null || video.ExerciseVideoMuxAssetId != assetId) return new(false, "no matching video");
            db.ExerciseVideos.Remove(video);
            return new(true, $"video {video.ExerciseVideoId} deleted");
        }

        return new(false, "type not handled");
    }

    /// <summary>
    /// Finds the video row by asset id, else by upload id, else by passthrough (= EXERCISE_VIDEO_ID), takes the
    /// exercise row lock and returns the row tracked (re-read under the lock).
    /// </summary>
    private async Task<ExerciseVideo?> FindAndLockAsync(
        string? assetId, string? uploadId, string? passthrough, CancellationToken ct)
    {
        Guid? exerciseId = null;
        if (!string.IsNullOrEmpty(assetId))
            exerciseId = await ExerciseIdWhere(v => v.ExerciseVideoMuxAssetId == assetId, ct);
        if (exerciseId is null && !string.IsNullOrEmpty(uploadId))
            exerciseId = await ExerciseIdWhere(v => v.ExerciseVideoMuxUploadId == uploadId, ct);
        if (exerciseId is null && Guid.TryParse(passthrough, out var videoId))
            exerciseId = await ExerciseIdWhere(v => v.ExerciseVideoId == videoId, ct);
        if (exerciseId is null)
            return null;

        if (!await MuxVideoRules.LockExerciseAsync(db, exerciseId.Value, ct))
            return null;
        return await db.ExerciseVideos.FirstOrDefaultAsync(v => v.ExerciseVideoExerciseId == exerciseId.Value, ct);
    }

    private async Task<Guid?> ExerciseIdWhere(
        System.Linq.Expressions.Expression<Func<ExerciseVideo, bool>> predicate, CancellationToken ct) =>
        await db.ExerciseVideos.AsNoTracking()
            .Where(predicate)
            .Select(v => (Guid?)v.ExerciseVideoExerciseId)
            .FirstOrDefaultAsync(ct);
}
