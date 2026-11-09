using Mentora.Core.Entities;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

/// <summary>
/// Lot 12.2a — runs a one-time data patch at most once per database: the patch and the insertion of
/// its DATA_PATCHES marker share one transaction (the primary key on the name refuses a second marker).
/// </summary>
internal static class DataPatchRunner
{
    private const string Tag = "[PATCH]";

    /// <param name="apply">Changes tracked entities (and may save them); returns a one-line summary.</param>
    public static async Task RunOnceAsync(MentoraDbContext db, ILogger logger, string name, Func<Task<string>> apply)
    {
        if (await db.DataPatches.AnyAsync(p => p.DataPatchName == name))
        {
            logger.LogInformation("{Tag} {Name} skipped (already applied)", Tag, name);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var summary = await apply();
        db.DataPatches.Add(new DataPatch { DataPatchName = name, DataPatchAppliedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation("{Tag} {Name} applied: {Summary}", Tag, name, summary);
    }
}
