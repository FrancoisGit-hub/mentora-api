using Mentora.Core.Entities;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

/// <summary>
/// Lot 12.2a one-time patch: links the free-text reference values of the Mentora exercises to the
/// closed reference lists, using REFERENCE_VALUE_MAP.csv (raw_value matched after NFC + trim + case folding).
/// For each list, when the foreign key is NULL and the text is not: a mapped value sets the foreign
/// key and rewrites the text to the reference label; an unmapped value (empty code or absent from
/// the map) leaves both unchanged and is reported. Secondary muscles: each element is mapped to a
/// MUSCLE_GROUPS row, the links are the distinct mapped groups minus the primary group, and the jsonb
/// mirror is rewritten as their names ordered by MUSCLE_GROUP_SORT_ORDER (NULL when none); unmapped
/// elements are dropped and reported. Coach-owned exercises are never touched, nor is
/// EXERCISE_UPDATED_DATE (the member and coach contract shows it). Runs after the catalog seeders.
/// </summary>
public static class ReferenceNormalizationPatch
{
    public const string Name = "Lot12_2a_ReferenceNormalization";
    private const string Tag = "[PATCH]";
    private const string MapFile = "REFERENCE_VALUE_MAP.csv";
    private const string SecondaryMuscle = "secondary_muscle";
    private static readonly string[] MapHeaders = ["list", "raw_value", "code"];

    private sealed record ListAccess(
        string List,
        Func<Exercise, string?> GetText, Action<Exercise, string> SetText,
        Func<Exercise, Guid?> GetId, Action<Exercise, Guid> SetId);

    private static readonly ListAccess[] Accessors =
    [
        new(ExerciseReferenceSeeder.Category,
            e => e.ExerciseCategory, (e, v) => e.ExerciseCategory = v,
            e => e.ExerciseCategoryId, (e, v) => e.ExerciseCategoryId = v),
        new(ExerciseReferenceSeeder.Type,
            e => e.ExerciseType, (e, v) => e.ExerciseType = v,
            e => e.ExerciseTypeId, (e, v) => e.ExerciseTypeId = v),
        new(ExerciseReferenceSeeder.Laterality,
            e => e.ExerciseLaterality, (e, v) => e.ExerciseLaterality = v,
            e => e.ExerciseLateralityId, (e, v) => e.ExerciseLateralityId = v),
        new(ExerciseReferenceSeeder.MovementPattern,
            e => e.ExerciseMovementPattern, (e, v) => e.ExerciseMovementPattern = v,
            e => e.ExerciseMovementPatternId, (e, v) => e.ExerciseMovementPatternId = v),
        new(ExerciseReferenceSeeder.StartingPosition,
            e => e.ExerciseStartingPosition, (e, v) => e.ExerciseStartingPosition = v,
            e => e.ExerciseStartingPositionId, (e, v) => e.ExerciseStartingPositionId = v),
        new(ExerciseReferenceSeeder.GripType,
            e => e.ExerciseGripType, (e, v) => e.ExerciseGripType = v,
            e => e.ExerciseGripTypeId, (e, v) => e.ExerciseGripTypeId = v),
    ];

    public static Task RunAsync(IServiceProvider services)
    {
        var db     = services.GetRequiredService<MentoraDbContext>();
        var logger = services.GetRequiredService<ILogger<MentoraDbContext>>();
        return DataPatchRunner.RunOnceAsync(db, logger, Name, () => ApplyAsync(db, logger));
    }

    private static async Task<string> ApplyAsync(MentoraDbContext db, ILogger logger)
    {
        var references = await LoadReferencesAsync(db);
        var groups = await db.MuscleGroups.ToListAsync();
        var groupsByName = groups.ToDictionary(g => g.MuscleGroupName, StringComparer.Ordinal);
        var map = ReadMap(references, groupsByName);

        var exercises = await db.Exercises
            .Where(e => e.ExerciseCoachId == null)
            .Include(e => e.SecondaryMuscleGroups)
            .OrderBy(e => e.ExerciseSourceCode).ThenBy(e => e.ExerciseName)
            .ToListAsync();

        // (list, raw value) -> source codes (or names when no code), for the report lines.
        var unmapped = new SortedDictionary<(string List, string Raw), List<string>>();
        var counts = new List<string>();

        foreach (var access in Accessors)
        {
            int mapped = 0, notMapped = 0;
            foreach (var exercise in exercises)
            {
                var text = access.GetText(exercise);
                if (access.GetId(exercise) != null || text is null)
                    continue;

                if (map[access.List].TryGetValue(SeedingText.ReferenceKey(text), out var code) && code != null)
                {
                    var (id, label) = references[access.List][code];
                    access.SetId(exercise, id);
                    access.SetText(exercise, label);
                    mapped++;
                }
                else
                {
                    notMapped++;
                    Append(unmapped, access.List, text, exercise);
                }
            }
            counts.Add($"{access.List} mapped={mapped} unmapped={notMapped}");
            logger.LogInformation("{Tag} {Patch} {List} mapped={Mapped} unmapped={Unmapped}", Tag, Name, access.List, mapped, notMapped);
        }

        int linksCreated = 0, dropped = 0, rewritten = 0;
        foreach (var exercise in exercises)
        {
            if (exercise.ExerciseSecondaryMuscles is null || exercise.SecondaryMuscleGroups.Count > 0)
                continue;

            var selected = new HashSet<Guid>();
            foreach (var element in exercise.ExerciseSecondaryMuscles)
            {
                if (map[SecondaryMuscle].TryGetValue(SeedingText.ReferenceKey(element), out var groupName) && groupName != null)
                    selected.Add(groupsByName[groupName].MuscleGroupId);
                else
                {
                    dropped++;
                    Append(unmapped, SecondaryMuscle, element, exercise);
                }
            }
            if (exercise.ExercisePrimaryMuscleGroupId is { } primary)
                selected.Remove(primary);

            foreach (var groupId in selected)
                exercise.SecondaryMuscleGroups.Add(new ExerciseSecondaryMuscleGroup { ExerciseSecondaryMuscleGroupMuscleGroupId = groupId });
            linksCreated += selected.Count;

            exercise.ExerciseSecondaryMuscles = selected.Count == 0
                ? null
                : groups.Where(g => selected.Contains(g.MuscleGroupId))
                    .OrderBy(g => g.MuscleGroupSortOrder).ThenBy(g => g.MuscleGroupName, StringComparer.Ordinal)
                    .Select(g => g.MuscleGroupName)
                    .ToList();
            rewritten++;
        }
        logger.LogInformation("{Tag} {Patch} secondary links created={Links} jsonb rewritten={Rewritten} elements dropped={Dropped}",
            Tag, Name, linksCreated, rewritten, dropped);

        foreach (var ((list, raw), codes) in unmapped)
            logger.LogInformation("{Tag} {Patch} unmapped {List} '{Raw}' x{Count}: {Codes}",
                Tag, Name, list, raw, codes.Count, string.Join(", ", codes));

        return $"{string.Join(", ", counts)}, secondary links created={linksCreated}, secondary elements dropped={dropped}";
    }

    private static void Append(SortedDictionary<(string, string), List<string>> unmapped, string list, string raw, Exercise exercise)
    {
        if (!unmapped.TryGetValue((list, raw), out var codes))
            unmapped[(list, raw)] = codes = [];
        codes.Add(exercise.ExerciseSourceCode ?? $"'{exercise.ExerciseName}'");
    }

    /// <summary>list -> code -> (id, label), from the six reference tables.</summary>
    private static async Task<Dictionary<string, Dictionary<string, (Guid Id, string Label)>>> LoadReferencesAsync(MentoraDbContext db)
    {
        static Dictionary<string, (Guid, string)> ToMap<T>(IEnumerable<T> rows, Func<T, string> code, Func<T, Guid> id, Func<T, string> label) =>
            rows.ToDictionary(code, r => (id(r), label(r)), StringComparer.Ordinal);

        return new Dictionary<string, Dictionary<string, (Guid Id, string Label)>>(StringComparer.Ordinal)
        {
            [ExerciseReferenceSeeder.Category] = ToMap(await db.ExerciseCategories.ToListAsync(),
                x => x.ExerciseCategoryCode, x => x.ExerciseCategoryId, x => x.ExerciseCategoryLabel),
            [ExerciseReferenceSeeder.Type] = ToMap(await db.ExerciseTypes.ToListAsync(),
                x => x.ExerciseTypeCode, x => x.ExerciseTypeId, x => x.ExerciseTypeLabel),
            [ExerciseReferenceSeeder.Laterality] = ToMap(await db.ExerciseLateralities.ToListAsync(),
                x => x.ExerciseLateralityCode, x => x.ExerciseLateralityId, x => x.ExerciseLateralityLabel),
            [ExerciseReferenceSeeder.MovementPattern] = ToMap(await db.MovementPatterns.ToListAsync(),
                x => x.MovementPatternCode, x => x.MovementPatternId, x => x.MovementPatternLabel),
            [ExerciseReferenceSeeder.StartingPosition] = ToMap(await db.StartingPositions.ToListAsync(),
                x => x.StartingPositionCode, x => x.StartingPositionId, x => x.StartingPositionLabel),
            [ExerciseReferenceSeeder.GripType] = ToMap(await db.GripTypes.ToListAsync(),
                x => x.GripTypeCode, x => x.GripTypeId, x => x.GripTypeLabel),
        };
    }

    /// <summary>
    /// list -> raw key (NFC + trim, case-insensitive) -> code (NULL = deliberately unmapped).
    /// Throws on an unknown list, a duplicate raw value or a code absent from its reference list.
    /// </summary>
    private static Dictionary<string, Dictionary<string, string?>> ReadMap(
        Dictionary<string, Dictionary<string, (Guid Id, string Label)>> references,
        Dictionary<string, MuscleGroupItem> groupsByName)
    {
        var map = new Dictionary<string, Dictionary<string, string?>>(StringComparer.Ordinal);
        foreach (var list in ExerciseReferenceSeeder.Lists.Append(SecondaryMuscle))
            map[list] = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var line = 1;
        foreach (var row in CatalogSeeder.ReadCsv(MapFile, MapHeaders))
        {
            line++;
            var list = CatalogSeeder.Clean(row["list"]);
            var raw  = CatalogSeeder.Clean(row["raw_value"]);
            var code = CatalogSeeder.Clean(row["code"]);

            if (list is null || !map.ContainsKey(list))
                throw new InvalidOperationException($"{MapFile} line {line}: unknown list '{list}'.");
            if (raw is null)
                throw new InvalidOperationException($"{MapFile} line {line}: empty raw_value.");
            if (code != null && list == SecondaryMuscle && !groupsByName.ContainsKey(code))
                throw new InvalidOperationException($"{MapFile} line {line}: '{code}' is not a MUSCLE_GROUPS name.");
            if (code != null && list != SecondaryMuscle && !references[list].ContainsKey(code))
                throw new InvalidOperationException($"{MapFile} line {line}: code '{code}' is not in list '{list}'.");
            if (!map[list].TryAdd(SeedingText.ReferenceKey(raw), code))
                throw new InvalidOperationException($"{MapFile} line {line}: duplicate raw_value '{raw}' in list '{list}'.");
        }
        return map;
    }
}
