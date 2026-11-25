using System.Text.Json;
using Mentora.Core.Entities;
using Mentora.Core.Notifications;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Services.Push;

/// <summary>
/// Lot 14.9b. Sends the PENDING rows of NOTIFICATIONS through FCM. Every <see cref="PollSeconds"/> seconds: one
/// transaction, up to <see cref="BatchSize"/> due rows locked with FOR UPDATE SKIP LOCKED, processed, saved,
/// committed. The whole loop body is wrapped: an error is logged (exception type only) and the loop continues
/// after a delay, the service never stops the host. Logs carry ids, counts and codes only, never a device
/// token, an access token or a message text.
/// </summary>
public sealed class PushNotificationSender(
    IServiceScopeFactory scopeFactory,
    ILogger<PushNotificationSender> logger) : BackgroundService
{
    public const int PollSeconds = 3;
    public const int MaxAttempts = 3;
    private const int BatchSize = 20;
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(24);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(PollSeconds);
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("[PUSH] sender loop error {ExceptionType}", ex.GetType().Name);
                delay = ErrorDelay;
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MentoraDbContext>();
        var fcm = scope.ServiceProvider.GetRequiredService<IFcmClient>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var rows = await db.Notifications
            .FromSqlRaw(
                """
                SELECT * FROM "NOTIFICATIONS"
                WHERE "NOTIFICATION_STATUS" = 'PENDING' AND "NOTIFICATION_NEXT_ATTEMPT_DATE" <= {0}
                ORDER BY "NOTIFICATION_CREATED_DATE"
                LIMIT {1}
                FOR UPDATE SKIP LOCKED
                """,
                DateTime.UtcNow, BatchSize)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return;

        // One query for the devices of every recipient of the batch.
        var userIds = rows.Select(r => r.NotificationUserId).Distinct().ToList();
        var devicesByUser = (await db.UserDevices
                .Where(d => userIds.Contains(d.UserId))
                .ToListAsync(ct))
            .GroupBy(d => d.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var row in rows)
        {
            devicesByUser.TryGetValue(row.NotificationUserId, out var devices);
            await ProcessAsync(db, fcm, row, devices ?? [], ct);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task ProcessAsync(
        MentoraDbContext db, IFcmClient fcm, Notification row, List<UserDevice> devices, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        int ok = 0, removed = 0, retryable = 0;
        FcmSendResult? lastFailure = null; // last non-retryable failure that did not remove the device

        if (row.NotificationCreatedDate < now - Expiry)
        {
            Finish(row, now, NotificationStatuses.Failed, NotificationErrors.Expired);
        }
        else if (!fcm.IsConfigured)
        {
            Finish(row, now, NotificationStatuses.Failed, NotificationErrors.NotConfigured);
        }
        else if (devices.Count == 0)
        {
            Finish(row, now, NotificationStatuses.NoDevice, null);
        }
        else
        {
            var data = ReadData(row);
            foreach (var device in devices)
            {
                var result = await SendSafelyAsync(fcm, device, row, data, ct);
                switch (result.Outcome)
                {
                    case FcmOutcome.Success:
                        ok++;
                        break;

                    case FcmOutcome.InvalidToken:
                        removed++;
                        await db.UserDevices.Where(d => d.UserDeviceId == device.UserDeviceId).ExecuteDeleteAsync(ct);
                        logger.LogInformation("[PUSH] device removed deviceId={DeviceId} reason={Reason}",
                            device.UserDeviceId, result.ErrorCode ?? "unknown");
                        break;

                    case FcmOutcome.Retryable:
                        retryable++;
                        logger.LogWarning("[PUSH] send retryable deviceId={DeviceId} http={Http} code={Code}",
                            device.UserDeviceId, result.HttpCode?.ToString() ?? "none", result.ErrorCode ?? "none");
                        break;

                    default:
                        lastFailure = result;
                        if (result.Field is not null)
                            // FCM refused our payload, not the device: the device row is kept.
                            logger.LogWarning("[PUSH] payload rejected notificationId={NotificationId} field={Field}",
                                row.NotificationId, result.Field);
                        else
                            logger.LogWarning("[PUSH] send failed deviceId={DeviceId} http={Http} code={Code}",
                                device.UserDeviceId, result.HttpCode?.ToString() ?? "none", result.ErrorCode ?? "none");
                        break;
                }
            }

            var attemptsMade = row.NotificationAttemptCount + 1;
            if (ok > 0)
            {
                Finish(row, now, NotificationStatuses.Sent, null);
                row.NotificationSentDate = now;
            }
            else if (retryable > 0 && attemptsMade < MaxAttempts)
            {
                // Stays PENDING: 1 minute after the first attempt, 5 minutes after the second.
                row.NotificationAttemptCount = attemptsMade;
                row.NotificationNextAttemptDate = now + (attemptsMade == 1 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(5));
                row.NotificationUpdatedDate = now;
            }
            else
            {
                row.NotificationAttemptCount = attemptsMade;
                Finish(row, now, NotificationStatuses.Failed, FinalError(devices.Count, removed, retryable, lastFailure));
            }
        }

        logger.LogInformation(
            "[PUSH] sent notificationId={NotificationId} type={Type} status={Status} devices={Devices} ok={Ok} removed={Removed}",
            row.NotificationId, row.NotificationType, row.NotificationStatus, devices.Count, ok, removed);
    }

    /// <summary>
    /// Error code when no device succeeded and no retry remains, in this order:
    /// 1. every device of the recipient was removed: ALL_TOKENS_INVALID;
    /// 2. at least one retryable failure (attempts exhausted): RETRIES_EXHAUSTED;
    /// 3. HTTP_code of the last non-retryable device failure, else EXCEPTION, else SEND_FAILED.
    /// </summary>
    internal static string FinalError(int deviceCount, int removed, int retryable, FcmSendResult? lastFailure)
    {
        if (deviceCount > 0 && removed == deviceCount)
            return NotificationErrors.AllTokensInvalid;
        if (retryable > 0)
            return NotificationErrors.RetriesExhausted;
        if (lastFailure?.HttpCode is { } code)
            return NotificationErrors.Http(code);
        if (lastFailure?.ErrorCode == NotificationErrors.Exception)
            return NotificationErrors.Exception;
        return NotificationErrors.SendFailed;
    }

    private async Task<FcmSendResult> SendSafelyAsync(
        IFcmClient fcm, UserDevice device, Notification row, Dictionary<string, string> data, CancellationToken ct)
    {
        try
        {
            return await fcm.SendAsync(device.UserDeviceToken, row.NotificationTitle, row.NotificationBody, data, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // An unexpected error must not make the whole batch loop forever: it counts as a failure of this device.
            logger.LogWarning("[PUSH] send error deviceId={DeviceId} {ExceptionType}", device.UserDeviceId, ex.GetType().Name);
            return new FcmSendResult(FcmOutcome.Failed, null, NotificationErrors.Exception);
        }
    }

    private static void Finish(Notification row, DateTime now, string status, string? error)
    {
        row.NotificationStatus = status;
        row.NotificationError = error;
        row.NotificationUpdatedDate = now;
    }

    /// <summary>The stored data plus notificationId, strings only, no null or empty value.</summary>
    private static Dictionary<string, string> ReadData(Notification row)
    {
        var data = new Dictionary<string, string>();
        using var doc = JsonDocument.Parse(row.NotificationData);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            var value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            if (!string.IsNullOrEmpty(value))
                data[property.Name] = value;
        }
        data["notificationId"] = row.NotificationId.ToString();
        return data;
    }
}
