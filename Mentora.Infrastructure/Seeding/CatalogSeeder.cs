using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

/// <summary>
/// Imports the exercise catalog from the four CSV exports of the programming sheet
/// (embedded resources, see Mentora.Infrastructure.csproj). Runs right after MuscleGroupSeeder.
///
/// Create-only, row by row: equipment by source code, objectives by name, exercises by source
/// code. Lot 12.2a: an existing exercise is never written (no NULL fill, no adoption by name, no
/// objective link added later), so an admin edit is never overwritten nor recreated. An active CSV
/// row is created, with its objective links, only when no exercise has its EX code; if its name is
/// already used by a Mentora exercise (accent- and case-insensitive) it is skipped with a warning.
/// The database is the source of truth of the catalog; the CSV only seeds missing rows.
///
/// Product rule: an exercise with no equipment is "Sans matériel" (see EquipmentItem.NoEquipmentName).
/// The row is get-or-created before the MATERIELS step; equipment named by exercises but absent
/// from MATERIELS is created right after it (category "Autre").
/// Everything runs in one transaction; a missing resource or header throws at startup.
/// </summary>
public static class CatalogSeeder
{
    private const string Tag = "[CATALOG]";
    private const string ResourcePrefix = "Mentora.Catalog.";

    private static readonly string[] ExerciseHeaders =
    [
        "exercise_id", "nom_exercice_fr", "nom_exercice_en", "categorie", "groupe_musculaire_principal",
        "muscles_secondaires", "pattern_mouvement", "type_exercice", "lateralite", "position_depart",
        "materiel_principal", "materiel_secondaire", "type_prise", "niveau_minimum", "objectifs_compatibles",
        "consigne_courte", "execution_detaillee", "respiration", "erreurs_frequentes", "precautions",
        "image_url", "gif_url", "video_url", "tags_recherche", "actif",
    ];
    private static readonly string[] EquipmentHeaders =
    [
        "materiel_id", "nom_materiel", "categorie", "sous_categorie", "usage_principal", "exercices_associes",
        "tests_associes", "necessite_reglage", "reglages_possibles", "actif", "notes_dev",
    ];
    private static readonly string[] MuscleHeaders =
    [
        "muscle_id", "groupe_musculaire", "muscle", "sous_partie", "region_anatomique", "fonction_principale",
        "fonction_secondaire", "articulations_associees", "actif", "notes_dev",
    ];
    private static readonly string[] ExerciseMuscleHeaders =
    [
        "exercise_muscle_id", "exercise_id", "muscle_id", "role", "importance",
    ];

    // Keyword -> legacy Equipment DB value. Order matters: first match wins.
    private static readonly (string Keyword, string Code)[] EquipmentKeywords =
    [
        ("haltère", "DUMBBELL"),
        ("barre", "BARBELL"), ("disque", "BARBELL"),
        ("kettlebell", "KETTLEBELL"),
        ("machine", "MACHINE"), ("presse", "MACHINE"),
        ("poulie", "CABLE"), ("câble", "CABLE"),
        ("élastique", "ELASTIC"), ("bande", "ELASTIC"),
        ("poids du corps", "BODYWEIGHT"), ("sans matériel", "BODYWEIGHT"),
    ];

    // Only used when materiel_principal matches no EQUIPMENT row (product-owner decision, 9.3).
    private static readonly (string Keyword, string Code)[] RawEquipmentKeywords =
        [.. EquipmentKeywords, ("aucun", "BODYWEIGHT")];

    // Column limits from ExerciseConfiguration / EquipmentItemConfiguration / ObjectiveConfiguration.
    private const int ExerciseNameMax = 120;
    private const int SourceCodeMax = 10;
    private const int EquipmentNameMax = 100;
    private const int EquipmentCategoryMax = 60;
    private const int ObjectiveNameMax = 60;

    private sealed record GroupResolution(MuscleGroupItem? Group, string? PrincipalMuscle, bool FromMuscles);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db     = services.GetRequiredService<MentoraDbContext>();
        var logger = services.GetRequiredService<ILogger<MentoraDbContext>>();

        // Read everything before touching the database: a missing resource or header throws here.
        var exerciseRows       = ReadCsv("EXERCICES.csv", ExerciseHeaders);
        var equipmentRows      = ReadCsv("MATERIELS.csv", EquipmentHeaders);
        var muscleRows         = ReadCsv("MUSCLES.csv", MuscleHeaders);
        var exerciseMuscleRows = ReadCsv("EXERCICES_MUSCLES.csv", ExerciseMuscleHeaders);

        var now = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync();

        var noEquipment = await EnsureNoEquipmentAsync(db, logger, now);
        var equipment = await SeedEquipmentAsync(db, logger, equipmentRows, now);

        var activeExercises = exerciseRows.Where(r => IsActive(r["actif"])).ToList();
        var objectives = await SeedObjectivesAsync(db, logger, activeExercises, now);

        // Rows sharing an exercise_id are ambiguous (their muscle rows are merged): all are skipped.
        var duplicateIds = activeExercises
            .GroupBy(r => Clean(r["exercise_id"]) ?? "")
            .Where(g => g.Key != "" && g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.Select(r => Clean(r["nom_exercice_fr"]) ?? "").ToList());
        var eligible = activeExercises
            .Where(r => Clean(r["exercise_id"]) is { } id && !duplicateIds.ContainsKey(id))
            .ToList();

        var groups = await ResolvePrimaryGroupsAsync(db, logger, eligible, muscleRows, exerciseMuscleRows);

        await SeedMissingEquipmentAsync(db, logger, eligible, equipment, now);

        await SeedExercisesAsync(db, logger, activeExercises, eligible, duplicateIds, groups, equipment, objectives, noEquipment, now);

        await transaction.CommitAsync();
    }

    // ── 3.0 "Sans matériel" ─────────────────────────────────────────────────────

    /// <summary>
    /// Get-or-create by name of the equipment row that stands for "no equipment needed".
    /// An existing row is never modified.
    /// </summary>
    private static async Task<EquipmentItem> EnsureNoEquipmentAsync(MentoraDbContext db, ILogger logger, DateTime now)
    {
        var existing = (await db.EquipmentItems.ToListAsync()).FirstOrDefault(e =>
            string.Equals(e.EquipmentName.Trim(), EquipmentItem.NoEquipmentName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            logger.LogInformation("CatalogSeeder no-equipment row: created 0, existing 1.");
            return existing;
        }

        var item = new EquipmentItem
        {
            EquipmentSourceCode  = null,
            EquipmentName        = EquipmentItem.NoEquipmentName,
            EquipmentCategory    = EquipmentItem.NoEquipmentName,
            EquipmentSubcategory = null,
            EquipmentLegacyCode  = "BODYWEIGHT",
            EquipmentIsActive    = true,
            EquipmentCreatedDate = now,
            EquipmentUpdatedDate = now,
        };
        db.EquipmentItems.Add(item);
        await db.SaveChangesAsync();
        logger.LogInformation("CatalogSeeder no-equipment row: created 1, existing 0.");
        return item;
    }

    /// <summary>materiel_principal / materiel_secondaire values meaning "no equipment" (empty and "-" arrive as null).</summary>
    private static bool IsNoEquipment(string? raw) =>
        raw is null
        || string.Equals(raw, "Poids du corps", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, EquipmentItem.NoEquipmentName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "Aucun", StringComparison.OrdinalIgnoreCase);

    // ── 3.1b Equipment named by exercises but missing from MATERIELS ────────────

    private static async Task SeedMissingEquipmentAsync(
        MentoraDbContext db, ILogger logger, List<Dictionary<string, string>> eligible,
        List<EquipmentItem> equipment, DateTime now)
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var plural = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in equipment)
        {
            exact.Add(e.EquipmentName.Trim().ToLowerInvariant());
            plural.Add(PluralKey(e.EquipmentName));
        }

        var createdNames = new List<string>();
        var warnings = new List<string>();

        foreach (var row in eligible)
        {
            foreach (var column in new[] { "materiel_principal", "materiel_secondaire" })
            {
                var raw = Clean(row[column]);
                if (raw is null || IsNoEquipment(raw))
                    continue;
                if (exact.Contains(raw.ToLowerInvariant()) || plural.Contains(PluralKey(raw)))
                    continue;
                if (raw.Length > EquipmentNameMax)
                {
                    warnings.Add($"equipment '{raw}' (exercise {Clean(row["exercise_id"])}): longer than {EquipmentNameMax} characters, skipped");
                    continue;
                }

                var item = new EquipmentItem
                {
                    EquipmentSourceCode  = null,
                    EquipmentName        = raw,
                    EquipmentCategory    = "Autre",
                    EquipmentSubcategory = null,
                    EquipmentLegacyCode  = MapEquipmentKeywords(EquipmentKeywords, raw) ?? "OTHER",
                    EquipmentIsActive    = true,
                    EquipmentCreatedDate = now,
                    EquipmentUpdatedDate = now,
                };
                db.EquipmentItems.Add(item);
                equipment.Add(item);
                exact.Add(raw.ToLowerInvariant());
                plural.Add(PluralKey(raw));
                createdNames.Add(raw);
            }
        }

        if (createdNames.Count > 0)
            await db.SaveChangesAsync();

        logger.LogInformation(
            "CatalogSeeder extra equipment: created {Created}, warnings {Warnings}.", createdNames.Count, warnings.Count);
        LogWarnings(logger, warnings);
        foreach (var name in createdNames)
            logger.LogInformation("{Tag} equipment created from exercises: '{Name}'", Tag, name);
    }

    // ── 3.1 Equipment ───────────────────────────────────────────────────────────

    private static async Task<List<EquipmentItem>> SeedEquipmentAsync(
        MentoraDbContext db, ILogger logger, List<Dictionary<string, string>> rows, DateTime now)
    {
        var all = await db.EquipmentItems.ToListAsync();
        var bySource = all.Where(e => e.EquipmentSourceCode != null)
            .ToDictionary(e => e.EquipmentSourceCode!, StringComparer.Ordinal);
        var names = all.Select(e => e.EquipmentName.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var active = rows.Where(r => IsActive(r["actif"])).ToList();
        var duplicateCodes = active.GroupBy(r => Clean(r["materiel_id"]) ?? "")
            .Where(g => g.Key != "" && g.Count() > 1).Select(g => g.Key).ToHashSet();

        var warnings = new List<string>();
        int created = 0, existing = 0, skipped = 0;

        foreach (var code in duplicateCodes)
            warnings.Add($"equipment {code}: source code used by several active rows, all skipped");

        foreach (var row in active)
        {
            var code        = Clean(row["materiel_id"]);
            var name        = Clean(row["nom_materiel"]);
            var category    = Clean(row["categorie"]);
            var subcategory = Clean(row["sous_categorie"]);

            if (code is null || name is null)
            {
                skipped++;
                warnings.Add($"equipment row skipped: missing materiel_id or nom_materiel ('{code}', '{name}')");
                continue;
            }
            if (duplicateCodes.Contains(code)) { skipped++; continue; }
            if (bySource.ContainsKey(code)) { existing++; continue; }

            if (code.Length > SourceCodeMax || name.Length > EquipmentNameMax
                || category?.Length > EquipmentCategoryMax || subcategory?.Length > EquipmentCategoryMax)
            {
                skipped++;
                warnings.Add($"equipment {code} '{name}': a value exceeds its column length, skipped");
                continue;
            }
            if (!names.Add(name))
            {
                skipped++;
                warnings.Add($"equipment {code} '{name}': name already used by another EQUIPMENT row, skipped");
                continue;
            }

            var item = new EquipmentItem
            {
                EquipmentSourceCode  = code,
                EquipmentName        = name,
                EquipmentCategory    = category,
                EquipmentSubcategory = subcategory,
                // Name first, then subcategory, then category: "Vis-à-vis | Machine à câble |
                // Double poulie" must resolve to CABLE, not MACHINE.
                EquipmentLegacyCode  = MapEquipmentKeywords(EquipmentKeywords, name, subcategory, category) ?? "OTHER",
                EquipmentIsActive    = true,
                EquipmentCreatedDate = now,
                EquipmentUpdatedDate = now,
            };
            db.EquipmentItems.Add(item);
            all.Add(item);
            bySource[code] = item;
            created++;
        }

        if (created > 0)
            await db.SaveChangesAsync();

        logger.LogInformation(
            "CatalogSeeder equipment: created {Created}, existing {Existing}, skipped {Skipped}, warnings {Warnings}.",
            created, existing, skipped, warnings.Count);
        LogWarnings(logger, warnings);
        return all;
    }

    // ── 3.2 Objectives ──────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, Objective>> SeedObjectivesAsync(
        MentoraDbContext db, ILogger logger, List<Dictionary<string, string>> activeExercises, DateTime now)
    {
        var byName = new Dictionary<string, Objective>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in await db.Objectives.ToListAsync())
            byName.TryAdd(o.ObjectiveName.Trim(), o);

        var warnings = new List<string>();
        int created = 0, existing = 0, skipped = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in activeExercises.SelectMany(r => SplitList(r["objectifs_compatibles"]) ?? []))
        {
            if (!seen.Add(name))
                continue;
            if (byName.ContainsKey(name)) { existing++; continue; }
            if (name.Length > ObjectiveNameMax)
            {
                skipped++;
                warnings.Add($"objective '{name}': longer than {ObjectiveNameMax} characters, skipped");
                continue;
            }

            var objective = new Objective
            {
                ObjectiveName        = name,
                ObjectiveCreatedDate = now,
                ObjectiveUpdatedDate = now,
            };
            db.Objectives.Add(objective);
            byName[name] = objective;
            created++;
        }

        if (created > 0)
            await db.SaveChangesAsync();

        logger.LogInformation(
            "CatalogSeeder objectives: created {Created}, existing {Existing}, skipped {Skipped}, warnings {Warnings}.",
            created, existing, skipped, warnings.Count);
        LogWarnings(logger, warnings);
        return byName;
    }

    // ── 3.3 Primary muscle group ────────────────────────────────────────────────

    private static async Task<Dictionary<string, GroupResolution>> ResolvePrimaryGroupsAsync(
        MentoraDbContext db, ILogger logger,
        List<Dictionary<string, string>> eligible,
        List<Dictionary<string, string>> muscleRows,
        List<Dictionary<string, string>> exerciseMuscleRows)
    {
        var groupsByName = new Dictionary<string, MuscleGroupItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in await db.MuscleGroups.ToListAsync())
            groupsByName.TryAdd(g.MuscleGroupName.Trim(), g);

        var musclesById = new Dictionary<string, (string? Group, string? Muscle)>(StringComparer.Ordinal);
        foreach (var m in muscleRows)
            if (Clean(m["muscle_id"]) is { } id)
                musclesById.TryAdd(id, (Clean(m["groupe_musculaire"]), Clean(m["muscle"])));

        // Principal row with the lowest importance per exercise (file order breaks ties).
        var principalByExercise = exerciseMuscleRows
            .Where(r => string.Equals(Clean(r["role"]), "Principal", StringComparison.OrdinalIgnoreCase)
                        && Clean(r["exercise_id"]) != null)
            .GroupBy(r => Clean(r["exercise_id"])!, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => Clean(g.OrderBy(r => int.TryParse(Clean(r["importance"]), out var i) ? i : int.MaxValue).First()["muscle_id"]),
                StringComparer.Ordinal);

        var result = new Dictionary<string, GroupResolution>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var mismatches = new List<string>();
        int fromMuscles = 0, fromLabel = 0, unresolved = 0;

        foreach (var row in eligible)
        {
            var id    = Clean(row["exercise_id"])!;
            var name  = Clean(row["nom_exercice_fr"]);
            var label = Clean(row["groupe_musculaire_principal"]);

            MuscleGroupItem? group = null;
            string? principalMuscle = null;
            if (principalByExercise.TryGetValue(id, out var muscleId) && muscleId != null
                && musclesById.TryGetValue(muscleId, out var muscle)
                && muscle.Group != null && groupsByName.TryGetValue(muscle.Group, out var derived))
            {
                group = derived;
                principalMuscle = muscle.Muscle;
            }

            if (group != null)
            {
                fromMuscles++;
                if (!string.Equals(label, group.MuscleGroupName, StringComparison.OrdinalIgnoreCase))
                    mismatches.Add($"group mismatch {id} '{name}': sheet label '{label}', derived '{group.MuscleGroupName}'");
                result[id] = new GroupResolution(group, principalMuscle, true);
            }
            else if (label != null && groupsByName.TryGetValue(label, out var byLabel))
            {
                fromLabel++;
                result[id] = new GroupResolution(byLabel, null, false);
            }
            else
            {
                unresolved++;
                warnings.Add($"exercise {id} '{name}': no primary muscle group (label '{label}'), left NULL");
                result[id] = new GroupResolution(null, null, false);
            }
        }

        logger.LogInformation(
            "CatalogSeeder primary groups: from muscles {FromMuscles}, from label {FromLabel}, unresolved {Unresolved}, mismatches {Mismatches}, warnings {Warnings}.",
            fromMuscles, fromLabel, unresolved, mismatches.Count, warnings.Count);
        LogWarnings(logger, warnings);
        LogWarnings(logger, mismatches);
        return result;
    }

    // ── 3.4 Exercises ───────────────────────────────────────────────────────────

    private static async Task SeedExercisesAsync(
        MentoraDbContext db, ILogger logger,
        List<Dictionary<string, string>> activeExercises,
        List<Dictionary<string, string>> eligible,
        Dictionary<string, List<string>> duplicateIds,
        Dictionary<string, GroupResolution> groups,
        List<EquipmentItem> equipment,
        Dictionary<string, Objective> objectives,
        EquipmentItem noEquipment,
        DateTime now)
    {
        // Read-only snapshot: existing exercises are never tracked nor written by this seeder.
        var sourceCodes = (await db.Exercises
                .Where(e => e.ExerciseSourceCode != null)
                .Select(e => e.ExerciseSourceCode!)
                .ToListAsync())
            .ToHashSet(StringComparer.Ordinal);
        var mentoraNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in await db.Exercises
                     .Where(e => e.ExerciseCoachId == null)
                     .Select(e => new { e.ExerciseName, e.ExerciseSourceCode })
                     .ToListAsync())
            mentoraNames.TryAdd(SeedingText.FoldedKey(e.ExerciseName), e.ExerciseSourceCode ?? e.ExerciseName);

        var equipmentExact = new Dictionary<string, EquipmentItem>(StringComparer.Ordinal);
        foreach (var e in equipment)
            equipmentExact.TryAdd(e.EquipmentName.Trim().ToLowerInvariant(), e);
        var equipmentPlural = equipment
            .GroupBy(e => PluralKey(e.EquipmentName))
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var duplicateNames = eligible
            .GroupBy(r => SeedingText.NormalizeName(Clean(r["nom_exercice_fr"]) ?? ""))
            .Where(g => g.Key != "" && g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.Select(r => Clean(r["exercise_id"])!).ToList());

        var warnings = new List<string>();
        // Aggregated per value so the product owner gets one line per unmatched label.
        var unmatchedEquipment = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var pluralMatches = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var ambiguousEquipment = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var unknownLevels = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        int created = 0, alreadyPresent = 0, skipped = 0, linksAdded = 0;

        foreach (var row in activeExercises.Where(r => Clean(r["exercise_id"]) is null))
        {
            skipped++;
            warnings.Add($"exercise row '{Clean(row["nom_exercice_fr"])}': empty exercise_id, skipped");
        }
        foreach (var (id, names) in duplicateIds)
        {
            skipped += names.Count;
            warnings.Add($"exercise {id}: source code used by {names.Count} active rows ({string.Join(", ", names.Select(n => $"'{n}'"))}), all skipped");
        }
        foreach (var (name, ids) in duplicateNames)
            warnings.Add($"exercises {string.Join(", ", ids)}: same normalized name '{name}', all skipped");

        foreach (var row in eligible)
        {
            var id   = Clean(row["exercise_id"])!;
            var name = Clean(row["nom_exercice_fr"]);

            // Create-only: an exercise holding this source code (Mentora or not, active or not) is never touched.
            if (sourceCodes.Contains(id)) { alreadyPresent++; continue; }

            if (name is null)
            {
                skipped++;
                warnings.Add($"exercise {id}: empty nom_exercice_fr, skipped");
                continue;
            }
            var normalized = SeedingText.NormalizeName(name);
            if (duplicateNames.ContainsKey(normalized)) { skipped++; continue; }

            var tooLong = new List<string>();
            CheckLength(tooLong, "exercise_id", id, SourceCodeMax);
            CheckLength(tooLong, "nom_exercice_fr", name, ExerciseNameMax);
            CheckLength(tooLong, "nom_exercice_en", Clean(row["nom_exercice_en"]), 120);
            CheckLength(tooLong, "categorie", Clean(row["categorie"]), 60);
            CheckLength(tooLong, "pattern_mouvement", Clean(row["pattern_mouvement"]), 60);
            CheckLength(tooLong, "type_exercice", Clean(row["type_exercice"]), 60);
            CheckLength(tooLong, "lateralite", Clean(row["lateralite"]), 40);
            CheckLength(tooLong, "position_depart", Clean(row["position_depart"]), 80);
            CheckLength(tooLong, "type_prise", Clean(row["type_prise"]), 60);
            if (tooLong.Count > 0)
            {
                skipped++;
                warnings.Add($"exercise {id} '{name}': too long for its column ({string.Join(", ", tooLong)}), skipped, never truncated");
                continue;
            }

            var folded = SeedingText.FoldedKey(name);
            if (mentoraNames.TryGetValue(folded, out var owner))
            {
                skipped++;
                warnings.Add($"exercise {id} '{name}': name already used by Mentora exercise {owner}, skipped");
                continue;
            }

            var resolution = groups[id];
            var primaryRaw = Clean(row["materiel_principal"]);
            var primary    = ResolvePrimary(primaryRaw, id);
            var secondary  = ResolveSecondary(Clean(row["materiel_secondaire"]), id);
            var level      = MapLevel(Clean(row["niveau_minimum"]), id);
            var category   = Clean(row["categorie"]);
            var type       = Clean(row["type_exercice"]);

            // Free-text reference values are stored raw; the Lot12_2a_ReferenceNormalization patch
            // (run after the seeders) links them to the reference tables on the first startup.
            var target = new Exercise
            {
                ExerciseCoachId              = null,
                ExerciseSourceCode           = id,
                ExerciseName                 = name,
                ExerciseNameEn               = Clean(row["nom_exercice_en"]),
                ExerciseCategory             = category,
                ExercisePrimaryMuscleGroupId = resolution.Group?.MuscleGroupId,
                ExerciseSecondaryMuscles     = SplitList(row["muscles_secondaires"]),
                ExerciseMovementPattern      = Clean(row["pattern_mouvement"]),
                ExerciseType                 = type,
                ExerciseLaterality           = Clean(row["lateralite"]),
                ExerciseStartingPosition     = Clean(row["position_depart"]),
                ExerciseGripType             = Clean(row["type_prise"]),
                ExercisePrimaryEquipmentId   = primary?.EquipmentId,
                ExerciseSecondaryEquipmentId = secondary?.EquipmentId,
                ExerciseMinLevel             = level,
                ExerciseDescription          = Clean(row["consigne_courte"]),
                ExerciseInstructions         = Clean(row["execution_detaillee"]),
                ExerciseBreathing            = Clean(row["respiration"]),
                ExerciseCommonMistakes       = SplitList(row["erreurs_frequentes"]),
                ExercisePrecautions          = Clean(row["precautions"]),
                ExerciseSearchTags           = Clean(row["tags_recherche"]),
                ExerciseCoachTip             = null,
                ExerciseMuscleGroup          = LegacyMuscleGroup(category, resolution),
                ExerciseEquipment            = LegacyEquipment(primary, primaryRaw),
                ExerciseIsPolyarticular      = string.Equals(type, "Polyarticulaire", StringComparison.OrdinalIgnoreCase),
                ExerciseIsActive             = true,
                ExerciseCreatedDate          = now,
                ExerciseUpdatedDate          = now,
            };
            db.Exercises.Add(target);
            created++;
            linksAdded += AddObjectiveLinks(target, row);

            sourceCodes.Add(id);
            mentoraNames[folded] = id;
        }

        foreach (var (value, ids) in unmatchedEquipment)
            warnings.Add($"unmatched equipment '{value}' x{ids.Count}, left NULL: {string.Join(", ", ids)}");
        foreach (var (value, ids) in ambiguousEquipment)
            warnings.Add($"ambiguous equipment '{value}' x{ids.Count}, left NULL: {string.Join(", ", ids)}");
        foreach (var (value, ids) in unknownLevels)
            warnings.Add($"unknown level '{value}' x{ids.Count}, left NULL: {string.Join(", ", ids)}");
        var pluralLines = pluralMatches
            .Select(p => $"plural-only equipment match {p.Key} x{p.Value.Count}: {string.Join(", ", p.Value)}")
            .ToList();

        if (created > 0)
            await db.SaveChangesAsync();

        logger.LogInformation(
            "CatalogSeeder exercises: created {Created}, already present {AlreadyPresent}, skipped {Skipped}, objective links created {Links}, plural-only equipment matches {Plural}, warnings {Warnings}.",
            created, alreadyPresent, skipped, linksAdded, pluralLines.Count, warnings.Count);
        LogWarnings(logger, warnings);
        LogWarnings(logger, pluralLines);

        // Primary equipment: an empty or no-equipment value means "Sans matériel".
        EquipmentItem? ResolvePrimary(string? raw, string exerciseId) =>
            IsNoEquipment(raw) ? noEquipment : MatchEquipment(raw, exerciseId, "primary");

        // Secondary equipment: no-equipment values stay NULL (no second "Sans matériel" link).
        EquipmentItem? ResolveSecondary(string? raw, string exerciseId) =>
            IsNoEquipment(raw) ? null : MatchEquipment(raw, exerciseId, "secondary");

        // Objective links of a newly created exercise only (never added to an existing one).
        int AddObjectiveLinks(Exercise target, Dictionary<string, string> row)
        {
            var added = 0;
            foreach (var objectiveName in SplitList(row["objectifs_compatibles"]) ?? [])
            {
                if (!objectives.TryGetValue(objectiveName, out var objective))
                    continue; // skipped in 3.2, already warned there
                if (target.ExerciseObjectives.Any(l => l.ExerciseObjectiveObjectiveId == objective.ObjectiveId))
                    continue;
                target.ExerciseObjectives.Add(new ExerciseObjective { ExerciseObjectiveObjectiveId = objective.ObjectiveId });
                added++;
            }
            return added;
        }

        EquipmentItem? MatchEquipment(string? raw, string exerciseId, string slot)
        {
            if (raw is null)
                return null;
            if (equipmentExact.TryGetValue(raw.ToLowerInvariant(), out var exact))
                return exact;
            if (equipmentPlural.TryGetValue(PluralKey(raw), out var plural))
            {
                if (plural.Count == 1)
                {
                    Append(pluralMatches, $"'{raw}' -> '{plural[0].EquipmentName}'", exerciseId);
                    return plural[0];
                }
                Append(ambiguousEquipment, raw, $"{exerciseId} ({slot})");
                return null;
            }
            Append(unmatchedEquipment, raw, $"{exerciseId} ({slot})");
            return null;
        }

        ExerciseLevel? MapLevel(string? raw, string exerciseId)
        {
            if (raw is null) return null;
            if (string.Equals(raw, "Débutant", StringComparison.OrdinalIgnoreCase))      return ExerciseLevel.Beginner;
            if (string.Equals(raw, "Intermédiaire", StringComparison.OrdinalIgnoreCase)) return ExerciseLevel.Intermediate;
            if (string.Equals(raw, "Avancé", StringComparison.OrdinalIgnoreCase))        return ExerciseLevel.Advanced;
            Append(unknownLevels, raw, exerciseId);
            return null;
        }
    }

    // ── Legacy columns (new exercises only) ─────────────────────────────────────

    private static MuscleGroup LegacyMuscleGroup(string? category, GroupResolution resolution)
    {
        if (category != null && category.Contains("cardio", StringComparison.OrdinalIgnoreCase))
            return MuscleGroup.Cardio;
        if (resolution.Group is { } group)
        {
            if (string.Equals(group.MuscleGroupName, "Bras", StringComparison.OrdinalIgnoreCase)
                && resolution.PrincipalMuscle?.Contains("triceps", StringComparison.OrdinalIgnoreCase) == true)
                return MuscleGroup.Triceps;
            if (MuscleGroupFromCode(group.MuscleGroupLegacyCode) is { } legacy)
                return legacy;
        }
        return MuscleGroup.FullBody;
    }

    private static Equipment LegacyEquipment(EquipmentItem? primary, string? primaryRaw)
    {
        if (primary != null)
            return EquipmentFromCode(primary.EquipmentLegacyCode) ?? Equipment.Other;
        if (primaryRaw is null)
            return Equipment.Bodyweight;
        return EquipmentFromCode(MapEquipmentKeywords(RawEquipmentKeywords, primaryRaw)) ?? Equipment.Other;
    }

    private static string? MapEquipmentKeywords((string Keyword, string Code)[] keywords, params string?[] fields)
    {
        foreach (var field in fields)
        {
            if (field is null) continue;
            var lower = field.ToLowerInvariant();
            foreach (var (keyword, code) in keywords)
                if (lower.Contains(keyword, StringComparison.Ordinal))
                    return code;
        }
        return null;
    }

    private static MuscleGroup? MuscleGroupFromCode(string? code) => code switch
    {
        "CHEST"      => MuscleGroup.Chest,
        "BACK"       => MuscleGroup.Back,
        "SHOULDERS"  => MuscleGroup.Shoulders,
        "BICEPS"     => MuscleGroup.Biceps,
        "TRICEPS"    => MuscleGroup.Triceps,
        "FOREARMS"   => MuscleGroup.Forearms,
        "ABS"        => MuscleGroup.Abs,
        "QUADRICEPS" => MuscleGroup.Quadriceps,
        "HAMSTRINGS" => MuscleGroup.Hamstrings,
        "GLUTES"     => MuscleGroup.Glutes,
        "CALVES"     => MuscleGroup.Calves,
        "FULL_BODY"  => MuscleGroup.FullBody,
        "CARDIO"     => MuscleGroup.Cardio,
        _            => null,
    };

    private static Equipment? EquipmentFromCode(string? code) => code switch
    {
        "BODYWEIGHT" => Equipment.Bodyweight,
        "BARBELL"    => Equipment.Barbell,
        "DUMBBELL"   => Equipment.Dumbbell,
        "KETTLEBELL" => Equipment.Kettlebell,
        "MACHINE"    => Equipment.Machine,
        "CABLE"      => Equipment.Cable,
        "ELASTIC"    => Equipment.Elastic,
        "OTHER"      => Equipment.Other,
        _            => null,
    };

    // ── CSV and text helpers ────────────────────────────────────────────────────

    /// <summary>Strict reader of an embedded catalog CSV (also used by the Lot 12.2a reference seeder and patch).</summary>
    internal static List<Dictionary<string, string>> ReadCsv(string fileName, string[] requiredHeaders)
    {
        var resourceName = ResourcePrefix + fileName;
        using var stream = typeof(CatalogSeeder).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Catalog resource '{resourceName}' is not embedded in Mentora.Infrastructure.");
        // Strict UTF-8: an invalid byte throws instead of producing replacement characters.
        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            DetectColumnCountChanges = true,
        });

        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
            throw new InvalidOperationException($"Catalog resource '{fileName}' has no header row.");
        var headers = csv.HeaderRecord;
        var missing = requiredHeaders.Except(headers, StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Catalog resource '{fileName}' is missing header(s): {string.Join(", ", missing)}.");

        var rows = new List<Dictionary<string, string>>();
        while (csv.Read())
        {
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var header in headers)
                row[header] = csv.GetField(header) ?? "";
            rows.Add(row);
        }
        return rows;
    }

    private static bool IsActive(string value) =>
        string.Equals(value.Trim(), "Oui", StringComparison.OrdinalIgnoreCase);

    /// <summary>Trimmed, NFC-normalized value; NULL for empty and "-".</summary>
    internal static string? Clean(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Normalize(NormalizationForm.FormC).Trim();
        return trimmed.Length == 0 || trimmed == "-" ? null : trimmed;
    }

    /// <summary>Splits a " ; " multi-valued cell; NULL when no value remains.</summary>
    private static List<string>? SplitList(string? value)
    {
        var items = (value ?? "").Split(';')
            .Select(Clean)
            .Where(v => v != null)
            .Select(v => v!)
            .ToList();
        return items.Count == 0 ? null : items;
    }

    /// <summary>Lowercased, trimmed, one trailing "s" removed from each word ("Haltères" == "Haltère").</summary>
    private static string PluralKey(string value) =>
        string.Join(' ', SeedingText.NormalizeName(value).Split(' ').Select(w => w.Length > 1 && w.EndsWith('s') ? w[..^1] : w));

    private static void CheckLength(List<string> tooLong, string column, string? value, int max)
    {
        if (value?.Length > max)
            tooLong.Add($"{column} {value.Length}>{max}");
    }

    private static void Append(SortedDictionary<string, List<string>> map, string key, string value)
    {
        if (!map.TryGetValue(key, out var list))
            map[key] = list = [];
        list.Add(value);
    }

    private static void LogWarnings(ILogger logger, List<string> lines)
    {
        foreach (var line in lines)
            logger.LogWarning("{Tag} {Line}", Tag, line);
    }
}
