using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

/// <summary>
/// Lot 12.2a one-time patch: a Mentora exercise with a NULL source code whose name is exactly one of
/// the ExerciseSeeder defaults receives that default's LG code, so that ExerciseSeeder recognises it
/// by code from now on (an admin rename no longer recreates it). Runs before CatalogSeeder.
/// EXERCISE_UPDATED_DATE is not touched (the member and coach contract shows it).
/// </summary>
public static class LegacyExerciseCodePatch
{
    public const string Name = "Lot12_2a_LegacyExerciseCodes";

    public static Task RunAsync(IServiceProvider services)
    {
        var db     = services.GetRequiredService<MentoraDbContext>();
        var logger = services.GetRequiredService<ILogger<MentoraDbContext>>();

        return DataPatchRunner.RunOnceAsync(db, logger, Name, async () =>
        {
            var codesByName = ExerciseSeeder.LegacyCodesByName();
            var usedCodes = (await db.Exercises
                    .Where(e => e.ExerciseSourceCode != null)
                    .Select(e => e.ExerciseSourceCode!)
                    .ToListAsync())
                .ToHashSet(StringComparer.Ordinal);
            var candidates = await db.Exercises
                .Where(e => e.ExerciseCoachId == null && e.ExerciseSourceCode == null)
                .ToListAsync();

            int given = 0, codeAlreadyUsed = 0;
            foreach (var exercise in candidates)
            {
                if (!codesByName.TryGetValue(exercise.ExerciseName, out var code))
                    continue;
                if (!usedCodes.Add(code))
                {
                    codeAlreadyUsed++;
                    logger.LogWarning("[PATCH] {Patch}: '{Name}' not coded, {Code} already used.", Name, exercise.ExerciseName, code);
                    continue;
                }
                exercise.ExerciseSourceCode = code;
                given++;
            }

            return $"codes given={given} candidates={candidates.Count} code already used={codeAlreadyUsed}";
        });
    }
}
