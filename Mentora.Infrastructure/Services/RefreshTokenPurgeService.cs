using Mentora.Core.Options;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services;

/// <summary>
/// Daily purge of AUTH_REFRESH_TOKENS rows that have been revoked or expired for more
/// than <see cref="RetentionDays"/> days. Waits
/// <see cref="RefreshTokenPurgeOptions.InitialDelayMinutes"/> before its first pass so
/// a container restart during a deployment never triggers a delete, then runs every
/// <see cref="Interval"/>. Governed by <see cref="RefreshTokenPurgeOptions.CountOnly"/>
/// (default true): count-only mode logs the row count the purge would delete and
/// deletes nothing. Live mode selects the eligible ids first and logs them (capped)
/// before deleting by id, so the first real purge leaves an audit trail rather than a
/// single opaque set-based DELETE.
/// </summary>
public class RefreshTokenPurgeService(
    IServiceScopeFactory scopeFactory,
    IOptions<RefreshTokenPurgeOptions> options,
    ILogger<RefreshTokenPurgeService> logger) : BackgroundService
{
    private const int RetentionDays = 30;
    private const int LoggedIdCap = 50;

    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(options.Value.InitialDelayMinutes), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(Interval, stoppingToken);
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

        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

        var eligible = db.AuthRefreshTokens.Where(t =>
            t.AuthRefreshTokenExpirationDate <= cutoff ||
            (t.AuthRefreshTokenIsRevoked && t.AuthRefreshTokenRevokedDate <= cutoff));

        var ids = await eligible.Select(t => t.AuthRefreshTokenId).ToListAsync(ct);

        if (options.Value.CountOnly)
        {
            logger.LogInformation(
                "RefreshTokenPurgeService (count-only): {Count} AUTH_REFRESH_TOKENS row(s) " +
                "expired or revoked before {Cutoff:O} would be deleted. Nothing deleted. " +
                "First {Cap} id(s): {Ids}",
                ids.Count, cutoff, LoggedIdCap, ids.Take(LoggedIdCap));
            return;
        }

        logger.LogInformation(
            "RefreshTokenPurgeService: deleting {Count} AUTH_REFRESH_TOKENS row(s) " +
            "expired or revoked before {Cutoff:O}. First {Cap} id(s): {Ids}",
            ids.Count, cutoff, LoggedIdCap, ids.Take(LoggedIdCap));

        if (ids.Count == 0)
            return;

        var deleted = await db.AuthRefreshTokens
            .Where(t => ids.Contains(t.AuthRefreshTokenId))
            .ExecuteDeleteAsync(ct);

        logger.LogInformation(
            "RefreshTokenPurgeService: deleted {Count} AUTH_REFRESH_TOKENS row(s).",
            deleted);
    }
}
