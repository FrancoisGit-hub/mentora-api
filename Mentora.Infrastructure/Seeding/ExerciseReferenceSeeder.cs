using System.Globalization;
using Mentora.Core.Entities;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

/// <summary>
/// Lot 12.2a — fills the six closed reference lists from EXERCISE_REFERENCES.csv (embedded resource,
/// same strict reader as CatalogSeeder). Create-only, keyed by (list, code): an existing row is never
/// updated nor deleted, so an admin edit of a label or sort order survives every startup.
/// A malformed file (unknown list, empty value, bad sort order, duplicate code) throws at startup.
/// </summary>
public static class ExerciseReferenceSeeder
{
    private const string Tag = "[CATALOG]";
    internal const string FileName = "EXERCISE_REFERENCES.csv";
    private static readonly string[] Headers = ["list", "code", "label", "sort_order"];

    internal const string Category         = "category";
    internal const string Type             = "type";
    internal const string Laterality       = "laterality";
    internal const string MovementPattern  = "movement_pattern";
    internal const string StartingPosition = "starting_position";
    internal const string GripType         = "grip_type";

    internal static readonly string[] Lists = [Category, Type, Laterality, MovementPattern, StartingPosition, GripType];

    internal sealed record ReferenceRow(string List, string Code, string Label, int SortOrder);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db     = services.GetRequiredService<MentoraDbContext>();
        var logger = services.GetRequiredService<ILogger<MentoraDbContext>>();

        var rows = ReadReferences();
        var now = DateTime.UtcNow;

        var existing = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [Category]         = (await db.ExerciseCategories.Select(x => x.ExerciseCategoryCode).ToListAsync()).ToHashSet(StringComparer.Ordinal),
            [Type]             = (await db.ExerciseTypes.Select(x => x.ExerciseTypeCode).ToListAsync()).ToHashSet(StringComparer.Ordinal),
            [Laterality]       = (await db.ExerciseLateralities.Select(x => x.ExerciseLateralityCode).ToListAsync()).ToHashSet(StringComparer.Ordinal),
            [MovementPattern]  = (await db.MovementPatterns.Select(x => x.MovementPatternCode).ToListAsync()).ToHashSet(StringComparer.Ordinal),
            [StartingPosition] = (await db.StartingPositions.Select(x => x.StartingPositionCode).ToListAsync()).ToHashSet(StringComparer.Ordinal),
            [GripType]         = (await db.GripTypes.Select(x => x.GripTypeCode).ToListAsync()).ToHashSet(StringComparer.Ordinal),
        };

        int created = 0, kept = 0;
        foreach (var row in rows)
        {
            if (existing[row.List].Contains(row.Code)) { kept++; continue; }
            Add(db, row, now);
            created++;
        }

        if (created > 0)
            await db.SaveChangesAsync();

        logger.LogInformation("{Tag} references created={Created} existing={Existing}", Tag, created, kept);
    }

    /// <summary>Reads and validates EXERCISE_REFERENCES.csv; throws on any malformed row.</summary>
    internal static List<ReferenceRow> ReadReferences()
    {
        var result = new List<ReferenceRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var line = 1;
        foreach (var raw in CatalogSeeder.ReadCsv(FileName, Headers))
        {
            line++;
            var list  = CatalogSeeder.Clean(raw["list"]);
            var code  = CatalogSeeder.Clean(raw["code"]);
            var label = CatalogSeeder.Clean(raw["label"]);
            var sort  = CatalogSeeder.Clean(raw["sort_order"]);

            if (list is null || !Lists.Contains(list, StringComparer.Ordinal))
                throw new InvalidOperationException($"{FileName} line {line}: unknown list '{list}'.");
            if (code is null || label is null)
                throw new InvalidOperationException($"{FileName} line {line}: empty code or label.");
            if (!int.TryParse(sort, NumberStyles.None, CultureInfo.InvariantCulture, out var sortOrder))
                throw new InvalidOperationException($"{FileName} line {line}: sort_order '{sort}' is not a non-negative integer.");
            if (!seen.Add(list + "|" + code))
                throw new InvalidOperationException($"{FileName} line {line}: duplicate code '{code}' in list '{list}'.");

            result.Add(new ReferenceRow(list, code, label, sortOrder));
        }
        return result;
    }

    private static void Add(MentoraDbContext db, ReferenceRow row, DateTime now)
    {
        if (row.List == Category)
            db.ExerciseCategories.Add(new ExerciseCategoryItem
            {
                ExerciseCategoryCode = row.Code, ExerciseCategoryLabel = row.Label, ExerciseCategorySortOrder = row.SortOrder,
                ExerciseCategoryIsActive = true, ExerciseCategoryCreatedDate = now, ExerciseCategoryUpdatedDate = now,
            });
        else if (row.List == Type)
            db.ExerciseTypes.Add(new ExerciseTypeItem
            {
                ExerciseTypeCode = row.Code, ExerciseTypeLabel = row.Label, ExerciseTypeSortOrder = row.SortOrder,
                ExerciseTypeIsActive = true, ExerciseTypeCreatedDate = now, ExerciseTypeUpdatedDate = now,
            });
        else if (row.List == Laterality)
            db.ExerciseLateralities.Add(new ExerciseLateralityItem
            {
                ExerciseLateralityCode = row.Code, ExerciseLateralityLabel = row.Label, ExerciseLateralitySortOrder = row.SortOrder,
                ExerciseLateralityIsActive = true, ExerciseLateralityCreatedDate = now, ExerciseLateralityUpdatedDate = now,
            });
        else if (row.List == MovementPattern)
            db.MovementPatterns.Add(new MovementPatternItem
            {
                MovementPatternCode = row.Code, MovementPatternLabel = row.Label, MovementPatternSortOrder = row.SortOrder,
                MovementPatternIsActive = true, MovementPatternCreatedDate = now, MovementPatternUpdatedDate = now,
            });
        else if (row.List == StartingPosition)
            db.StartingPositions.Add(new StartingPositionItem
            {
                StartingPositionCode = row.Code, StartingPositionLabel = row.Label, StartingPositionSortOrder = row.SortOrder,
                StartingPositionIsActive = true, StartingPositionCreatedDate = now, StartingPositionUpdatedDate = now,
            });
        else if (row.List == GripType)
            db.GripTypes.Add(new GripTypeItem
            {
                GripTypeCode = row.Code, GripTypeLabel = row.Label, GripTypeSortOrder = row.SortOrder,
                GripTypeIsActive = true, GripTypeCreatedDate = now, GripTypeUpdatedDate = now,
            });
        else
            throw new InvalidOperationException($"Unknown reference list '{row.List}'.");
    }
}
