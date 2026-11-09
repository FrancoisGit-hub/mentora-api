using System.Text;
using System.Text.RegularExpressions;
using Mentora.Core.Enums;
using Mentora.Core.Text;

namespace Mentora.Infrastructure.Seeding;

/// <summary>Lot 12.2a — text keys shared by the catalog seeders and data patches.</summary>
internal static class SeedingText
{
    /// <summary>NFC, trim, lower case, whitespace collapsed (moved unchanged from CatalogSeeder).</summary>
    public static string NormalizeName(string value) =>
        Regex.Replace(value.Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant(), @"\s+", " ");

    /// <summary>
    /// Name key that also ignores accents: NFC, AccentFolding (same table as the SQL search, lower case),
    /// trim, whitespace collapsed. Used to refuse a new Mentora exercise whose name is already taken.
    /// </summary>
    public static string FoldedKey(string value) =>
        Regex.Replace(AccentFolding.Fold(value.Normalize(NormalizationForm.FormC)).Trim(), @"\s+", " ");

    /// <summary>
    /// Key of a free-text reference value: NFC + trim. Compare with StringComparer.OrdinalIgnoreCase
    /// (case folding), as REFERENCE_VALUE_MAP.csv is matched.
    /// </summary>
    public static string ReferenceKey(string value) => value.Normalize(NormalizationForm.FormC).Trim();
}

/// <summary>Lot 12.2a — string comparer on <see cref="SeedingText.NormalizeName"/>.</summary>
internal sealed class NormalizedNameComparer : IEqualityComparer<string>
{
    public static readonly NormalizedNameComparer Instance = new();

    public bool Equals(string? x, string? y) =>
        x is null || y is null ? ReferenceEquals(x, y) : SeedingText.NormalizeName(x) == SeedingText.NormalizeName(y);

    public int GetHashCode(string obj) => StringComparer.Ordinal.GetHashCode(SeedingText.NormalizeName(obj));
}

/// <summary>Lot 12.2a — legacy MuscleGroup code to MUSCLE_GROUPS name (rule 7.6, moved from CatalogSeeder).</summary>
internal static class LegacyMuscleGroupMapping
{
    /// <summary>MUSCLE_GROUPS name of a legacy code; FULL_BODY and CARDIO map to nothing.</summary>
    public static string? GroupName(MuscleGroup legacy)
    {
        if (legacy == MuscleGroup.Chest)      return "Pectoraux";
        if (legacy == MuscleGroup.Back)       return "Dos";
        if (legacy == MuscleGroup.Shoulders)  return "Épaules";
        if (legacy == MuscleGroup.Biceps)     return "Bras";
        if (legacy == MuscleGroup.Triceps)    return "Bras";
        if (legacy == MuscleGroup.Forearms)   return "Avant-bras";
        if (legacy == MuscleGroup.Abs)        return "Abdominaux";
        if (legacy == MuscleGroup.Quadriceps) return "Quadriceps";
        if (legacy == MuscleGroup.Hamstrings) return "Ischio-jambiers";
        if (legacy == MuscleGroup.Glutes)     return "Fessiers";
        if (legacy == MuscleGroup.Calves)     return "Mollets";
        return null;
    }
}
