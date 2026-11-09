using Mentora.Core.Entities;
using Mentora.Core.Enums;

namespace Mentora.Infrastructure.Seeding;

/// <summary>
/// Lot 12.2b — computation of the deprecated EXERCISE_MUSCLE_GROUP and EXERCISE_EQUIPMENT columns, shared by
/// the catalog seeder (moved unchanged from CatalogSeeder) and the admin exercise service.
/// </summary>
internal static class LegacyExerciseColumns
{
    /// <summary>
    /// Cardio when the category label says so, else the legacy code of the primary group (Bras becomes
    /// Triceps when the principal muscle text mentions triceps), else FULL_BODY.
    /// </summary>
    public static MuscleGroup MuscleGroupFor(string? categoryLabel, MuscleGroupItem? group, string? principalMuscle)
    {
        if (categoryLabel != null && categoryLabel.Contains("cardio", StringComparison.OrdinalIgnoreCase))
            return MuscleGroup.Cardio;
        if (group is not null)
        {
            if (string.Equals(group.MuscleGroupName, "Bras", StringComparison.OrdinalIgnoreCase)
                && principalMuscle?.Contains("triceps", StringComparison.OrdinalIgnoreCase) == true)
                return MuscleGroup.Triceps;
            if (MuscleGroupFromCode(group.MuscleGroupLegacyCode) is { } legacy)
                return legacy;
        }
        return MuscleGroup.FullBody;
    }

    /// <summary>Legacy equipment of a matched equipment row.</summary>
    public static Equipment EquipmentFor(EquipmentItem primary) =>
        EquipmentFromCode(primary.EquipmentLegacyCode) ?? Equipment.Other;

    public static MuscleGroup? MuscleGroupFromCode(string? code)
    {
        if (code == "CHEST")      return MuscleGroup.Chest;
        if (code == "BACK")       return MuscleGroup.Back;
        if (code == "SHOULDERS")  return MuscleGroup.Shoulders;
        if (code == "BICEPS")     return MuscleGroup.Biceps;
        if (code == "TRICEPS")    return MuscleGroup.Triceps;
        if (code == "FOREARMS")   return MuscleGroup.Forearms;
        if (code == "ABS")        return MuscleGroup.Abs;
        if (code == "QUADRICEPS") return MuscleGroup.Quadriceps;
        if (code == "HAMSTRINGS") return MuscleGroup.Hamstrings;
        if (code == "GLUTES")     return MuscleGroup.Glutes;
        if (code == "CALVES")     return MuscleGroup.Calves;
        if (code == "FULL_BODY")  return MuscleGroup.FullBody;
        if (code == "CARDIO")     return MuscleGroup.Cardio;
        return null;
    }

    public static Equipment? EquipmentFromCode(string? code)
    {
        if (code == "BODYWEIGHT") return Equipment.Bodyweight;
        if (code == "BARBELL")    return Equipment.Barbell;
        if (code == "DUMBBELL")   return Equipment.Dumbbell;
        if (code == "KETTLEBELL") return Equipment.Kettlebell;
        if (code == "MACHINE")    return Equipment.Machine;
        if (code == "CABLE")      return Equipment.Cable;
        if (code == "ELASTIC")    return Equipment.Elastic;
        if (code == "OTHER")      return Equipment.Other;
        return null;
    }
}
