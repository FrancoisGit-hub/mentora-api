using Mentora.Core.Entities;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

/// <summary>
/// Seeds the exercise-catalog muscle groups. Idempotent name by name (get-or-create):
/// existing rows are never updated, missing ones are created.
/// </summary>
public static class MuscleGroupSeeder
{
    private sealed record SeedMuscleGroup(string Name, string LegacyCode);

    // SORT_ORDER is the 1-based position in this list.
    private static readonly SeedMuscleGroup[] DefaultMuscleGroups =
    [
        new("Pectoraux",        "CHEST"),
        new("Dos",              "BACK"),
        new("Épaules",          "SHOULDERS"),
        new("Bras",             "BICEPS"),
        new("Avant-bras",       "FOREARMS"),
        new("Quadriceps",       "QUADRICEPS"),
        new("Ischio-jambiers",  "HAMSTRINGS"),
        new("Fessiers",         "GLUTES"),
        new("Adducteurs",       "GLUTES"),
        new("Mollets",          "CALVES"),
        new("Tibial",           "CALVES"),
        new("Abdominaux",       "ABS"),
        new("Centre du corps",  "ABS"),
        new("Hanche",           "GLUTES"),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db     = services.GetRequiredService<MentoraDbContext>();
        var logger = services.GetRequiredService<ILogger<MentoraDbContext>>();

        var existingNames = (await db.MuscleGroups
            .Select(m => m.MuscleGroupName)
            .ToListAsync()).ToHashSet();

        var now = DateTime.UtcNow;
        var created = 0;

        for (var i = 0; i < DefaultMuscleGroups.Length; i++)
        {
            var item = DefaultMuscleGroups[i];
            if (existingNames.Contains(item.Name))
                continue;

            db.MuscleGroups.Add(new MuscleGroupItem
            {
                MuscleGroupName        = item.Name,
                MuscleGroupSortOrder   = i + 1,
                MuscleGroupLegacyCode  = item.LegacyCode,
                MuscleGroupCreatedDate = now,
                MuscleGroupUpdatedDate = now,
            });
            created++;
        }

        if (created > 0)
            await db.SaveChangesAsync();

        logger.LogInformation("MuscleGroupSeeder: created {Count} muscle group(s).", created);
    }
}
