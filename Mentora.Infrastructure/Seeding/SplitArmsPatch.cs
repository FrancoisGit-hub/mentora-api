using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

/// <summary>
/// Lot 13bis.1 one-time patch: splits the muscle group "Bras" into "Biceps" and "Triceps".
/// The "Bras" row is renamed "Biceps" in place (same id, legacy code BICEPS, sort order unchanged) and a
/// "Triceps" row is created (legacy code TRICEPS, sort order 4). Fixed lists then move the primary group
/// and the secondary links of the Mentora exercises to "Triceps"; everything else stays on the renamed
/// row. The jsonb mirror of every exercise linked to Biceps or Triceps is rebuilt with the admin rule.
/// Like the Lot12_2a patches: no audit row, EXERCISE_UPDATED_DATE untouched, coach-owned exercises never
/// touched. Runs after ReferenceNormalizationPatch so new and existing databases end in the same state.
/// Throws (the API refuses to start) when "Bras" coexists with a "Biceps" or "Triceps" row.
/// </summary>
public static class SplitArmsPatch
{
    public const string Name = "Lot13bis_SplitArms";
    internal const string ArmsName = "Bras";
    private const string Tag = "[PATCH]";
    private const string BicepsName = "Biceps";
    private const string TricepsName = "Triceps";
    private const string BicepsLegacyCode = "BICEPS";
    private const string TricepsLegacyCode = "TRICEPS";
    private const int TricepsSortOrder = 4;
    // Same limit as the admin write validator (AdminExerciseValidators.cs:52-60).
    private const int MaxSecondaryGroups = 6;

    // The three code lists below come from the Sheet text of muscles_secondaires and from the PROD read
    // of 18/11 (decisions 13bis.C and 13bis.D). They apply to Mentora exercises only.

    /// <summary>13bis.C: primary group moves from "Bras" to "Triceps" (legacy column set to TRICEPS).</summary>
    private static readonly HashSet<string> PrimaryToTricepsCodes = new(StringComparer.Ordinal)
    {
        "EX009", "EX118", "EX119", "EX120", "EX121", "EX122", "EX123", "EX124", "EX125", "EX126", "EX127", "EX128",
        "LG18", "LG19",
    };

    /// <summary>13bis.D: the secondary link to "Bras" moves to "Triceps" (42 codes).</summary>
    private static readonly HashSet<string> SecondaryToTricepsCodes = new(StringComparer.Ordinal)
    {
        "EX001", "EX012", "EX228", "EX065", "EX066", "EX067",
        "EX075", "EX076", "EX077", "EX078", "EX079", "EX080", "EX081", "EX082", "EX083", "EX084", "EX085",
        "EX213", "EX220", "EX245",
        "EX016", "EX017", "EX018", "EX019", "EX020", "EX021", "EX022", "EX023", "EX024", "EX025", "EX026",
        "EX027", "EX028", "EX029", "EX030", "EX031", "EX032", "EX033", "EX034", "EX035", "EX036", "EX037",
    };

    /// <summary>13bis.D: the secondary link to "Bras" (now Biceps) is kept and a link to "Triceps" is added.</summary>
    private static readonly HashSet<string> SecondaryBothCodes = new(StringComparer.Ordinal)
    {
        "EX013", "EX227", "EX233", "EX237", "EX238",
    };

    public static Task RunAsync(IServiceProvider services)
    {
        var db     = services.GetRequiredService<MentoraDbContext>();
        var logger = services.GetRequiredService<ILogger<MentoraDbContext>>();
        return DataPatchRunner.RunOnceAsync(db, logger, Name, () => ApplyAsync(db, logger));
    }

    private static async Task<string> ApplyAsync(MentoraDbContext db, ILogger logger)
    {
        var groups = await db.MuscleGroups.ToListAsync();
        var arms = groups.SingleOrDefault(g => g.MuscleGroupName == ArmsName);
        if (arms is null)
        {
            logger.LogInformation("{Tag} {Patch} no '{Arms}' row, nothing changed", Tag, Name, ArmsName);
            return Summary(0, 0, 0, 0, 0, 0, 0, 0) + $" (no '{ArmsName}' row, nothing changed)";
        }

        var clash = groups
            .Where(g => string.Equals(g.MuscleGroupName, BicepsName, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(g.MuscleGroupName, TricepsName, StringComparison.OrdinalIgnoreCase))
            .Select(g => $"'{g.MuscleGroupName}'")
            .ToList();
        if (clash.Count > 0)
            throw new InvalidOperationException(
                $"{Name}: unexpected state, '{ArmsName}' exists together with {string.Join(" and ", clash)}. " +
                "The split is refused and nothing is merged; fix MUSCLE_GROUPS by hand first.");

        // 13bis.A: rename in place, create Triceps. Saved now (inside the runner transaction) to get its id.
        var now = DateTime.UtcNow;
        var biceps = arms;
        biceps.MuscleGroupName        = BicepsName;
        biceps.MuscleGroupLegacyCode  = BicepsLegacyCode;
        biceps.MuscleGroupUpdatedDate = now;
        var triceps = new MuscleGroupItem
        {
            MuscleGroupName        = TricepsName,
            MuscleGroupSortOrder   = TricepsSortOrder,
            MuscleGroupLegacyCode  = TricepsLegacyCode,
            MuscleGroupCreatedDate = now,
            MuscleGroupUpdatedDate = now,
        };
        db.MuscleGroups.Add(triceps);
        await db.SaveChangesAsync();
        groups.Add(triceps);
        var groupNames = groups.ToDictionary(g => g.MuscleGroupId, g => g.MuscleGroupName);

        var exercises = await db.Exercises
            .Where(e => e.ExerciseCoachId == null)
            .Include(e => e.SecondaryMuscleGroups)
            .OrderBy(e => e.ExerciseSourceCode).ThenBy(e => e.ExerciseName)
            .ToListAsync();

        int primaryToTriceps = 0, secondaryToTriceps = 0, secondaryBoth = 0, skipped = 0, mirrorsRewritten = 0, warnings = 0;

        // 13bis.C: primary group.
        foreach (var exercise in exercises)
        {
            if (exercise.ExerciseSourceCode is not { } code || !PrimaryToTricepsCodes.Contains(code))
                continue;
            if (exercise.ExercisePrimaryMuscleGroupId != biceps.MuscleGroupId)
            {
                logger.LogInformation("{Tag} {Patch} {Code} primary not applicable: primary group is {Group}, not {Arms}",
                    Tag, Name, code, GroupLabel(groupNames, exercise.ExercisePrimaryMuscleGroupId), ArmsName);
                continue;
            }
            exercise.ExercisePrimaryMuscleGroupId = triceps.MuscleGroupId;
            exercise.ExerciseMuscleGroup = MuscleGroup.Triceps;
            primaryToTriceps++;
            logger.LogInformation("{Tag} {Patch} {Code} primary {From} -> {To}", Tag, Name, code, ArmsName, TricepsName);
        }

        foreach (var exercise in exercises.Where(e => e.ExercisePrimaryMuscleGroupId == biceps.MuscleGroupId
                                                   && e.ExerciseMuscleGroup == MuscleGroup.Triceps))
        {
            warnings++;
            logger.LogWarning("{Tag} {Patch} {Code} warning: primary group stays {Biceps} but the legacy column is TRICEPS (not changed)",
                Tag, Name, Label(exercise), BicepsName);
        }

        // 13bis.D: secondary links, only on exercises linked to the former "Bras" row.
        foreach (var exercise in exercises)
        {
            var code = exercise.ExerciseSourceCode;
            var move = code != null && SecondaryToTricepsCodes.Contains(code);
            var both = code != null && SecondaryBothCodes.Contains(code);
            if (!move && !both)
                continue;

            var armsLink = exercise.SecondaryMuscleGroups
                .FirstOrDefault(l => l.ExerciseSecondaryMuscleGroupMuscleGroupId == biceps.MuscleGroupId);
            if (armsLink is null)
            {
                logger.LogInformation("{Tag} {Patch} {Code} secondary not applicable: no link to {Arms}", Tag, Name, code, ArmsName);
                continue;
            }

            var refusal = TricepsLinkRefusal(exercise, triceps.MuscleGroupId, removesArmsLink: move);
            if (refusal != null)
            {
                skipped++;
                logger.LogWarning("{Tag} {Patch} {Code} skipped: {Reason}", Tag, Name, code, refusal);
                continue;
            }

            if (move)
            {
                exercise.SecondaryMuscleGroups.Remove(armsLink);
                db.Remove(armsLink);
            }
            exercise.SecondaryMuscleGroups.Add(new ExerciseSecondaryMuscleGroup
            {
                ExerciseSecondaryMuscleGroupExerciseId    = exercise.ExerciseId,
                ExerciseSecondaryMuscleGroupMuscleGroupId = triceps.MuscleGroupId,
            });

            if (move)
            {
                secondaryToTriceps++;
                logger.LogInformation("{Tag} {Patch} {Code} secondary {From} -> {To}", Tag, Name, code, ArmsName, TricepsName);
            }
            else
            {
                secondaryBoth++;
                logger.LogInformation("{Tag} {Patch} {Code} both {From} -> {Biceps}+{Triceps}", Tag, Name, code, ArmsName, BicepsName, TricepsName);
            }
        }

        // 13bis.E: mirror, same expression as the admin service (AdminExerciseService.cs:806-809, 824).
        var splitIds = new HashSet<Guid> { biceps.MuscleGroupId, triceps.MuscleGroupId };
        foreach (var exercise in exercises)
        {
            var linked = exercise.SecondaryMuscleGroups.Select(l => l.ExerciseSecondaryMuscleGroupMuscleGroupId).ToHashSet();
            var linkedToSplit = linked.Overlaps(splitIds);
            var mirrorNamesArms = exercise.ExerciseSecondaryMuscles?.Contains(ArmsName) == true;
            if (!linkedToSplit && !mirrorNamesArms)
                continue;
            if (!linkedToSplit)
            {
                warnings++;
                logger.LogWarning("{Tag} {Patch} {Code} warning: mirror named {Arms} without a link (drift), rebuilt from the links",
                    Tag, Name, Label(exercise), ArmsName);
            }

            var names = groups
                .Where(g => linked.Contains(g.MuscleGroupId))
                .OrderBy(g => g.MuscleGroupSortOrder).ThenBy(g => g.MuscleGroupName, StringComparer.Ordinal)
                .Select(g => g.MuscleGroupName)
                .ToList();
            var mirror = names.Count == 0 ? null : names;

            var current = exercise.ExerciseSecondaryMuscles;
            if (current is null ? mirror is null : mirror is not null && current.SequenceEqual(mirror, StringComparer.Ordinal))
                continue;
            exercise.ExerciseSecondaryMuscles = mirror;
            mirrorsRewritten++;
        }

        return Summary(1, 1, primaryToTriceps, secondaryToTriceps, secondaryBoth, skipped, mirrorsRewritten, warnings);
    }

    /// <summary>Reason why a Triceps secondary link cannot be added, or null when it can.</summary>
    private static string? TricepsLinkRefusal(Exercise exercise, Guid tricepsId, bool removesArmsLink)
    {
        if (exercise.ExercisePrimaryMuscleGroupId == tricepsId)
            return $"{TricepsName} is the primary group (a secondary link cannot equal it)";
        if (exercise.SecondaryMuscleGroups.Any(l => l.ExerciseSecondaryMuscleGroupMuscleGroupId == tricepsId))
            return $"already linked to {TricepsName} (duplicate)";
        var countAfter = exercise.SecondaryMuscleGroups.Count - (removesArmsLink ? 1 : 0) + 1;
        if (countAfter > MaxSecondaryGroups)
            return $"would have {countAfter} secondary groups (max {MaxSecondaryGroups})";
        return null;
    }

    private static string Summary(int renamed, int created, int primaryToTriceps, int secondaryToTriceps, int secondaryBoth,
        int skipped, int mirrorsRewritten, int warnings) =>
        $"renamed={renamed} created={created} primaryToTriceps={primaryToTriceps} secondaryToTriceps={secondaryToTriceps} " +
        $"secondaryBoth={secondaryBoth} skipped={skipped} mirrorsRewritten={mirrorsRewritten} warnings={warnings}";

    private static string Label(Exercise exercise) => exercise.ExerciseSourceCode ?? $"'{exercise.ExerciseName}'";

    private static string GroupLabel(Dictionary<Guid, string> groupNames, Guid? id) =>
        id is { } value && groupNames.TryGetValue(value, out var name) ? name : "none";
}
