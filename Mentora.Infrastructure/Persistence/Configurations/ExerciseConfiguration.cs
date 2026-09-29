using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("EXERCISES");

        builder.HasKey(e => e.ExerciseId);
        builder.Property(e => e.ExerciseId)
            .HasColumnName("EXERCISE_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        // NULL means "Mentora catalogue entry" — no coach owns this row
        builder.Property(e => e.ExerciseCoachId)
            .HasColumnName("EXERCISE_COACH_ID");

        builder.Property(e => e.ExerciseName)
            .HasColumnName("EXERCISE_NAME")
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(e => e.ExerciseDescription)
            .HasColumnName("EXERCISE_DESCRIPTION")
            .HasColumnType("text");

        builder.Property(e => e.ExerciseInstructions)
            .HasColumnName("EXERCISE_INSTRUCTIONS")
            .HasColumnType("text");

        builder.Property(e => e.ExerciseVideoUrl)
            .HasColumnName("EXERCISE_VIDEO_URL")
            .HasMaxLength(500);

        builder.Property(e => e.ExerciseImageUrl)
            .HasColumnName("EXERCISE_IMAGE_URL")
            .HasMaxLength(500);

        var muscleGroupConverter = new ValueConverter<MuscleGroup, string>(
            v => MuscleGroupToDb(v),
            v => MuscleGroupFromDb(v));

        builder.Property(e => e.ExerciseMuscleGroup)
            .HasColumnName("EXERCISE_MUSCLE_GROUP")
            .IsRequired()
            .HasMaxLength(40)
            .HasConversion(muscleGroupConverter);

        var equipmentConverter = new ValueConverter<Equipment, string>(
            v => EquipmentToDb(v),
            v => EquipmentFromDb(v));

        builder.Property(e => e.ExerciseEquipment)
            .HasColumnName("EXERCISE_EQUIPMENT")
            .IsRequired()
            .HasMaxLength(40)
            .HasConversion(equipmentConverter);

        builder.Property(e => e.ExerciseIsPolyarticular)
            .HasColumnName("EXERCISE_IS_POLYARTICULAR")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.ExerciseIsActive)
            .HasColumnName("EXERCISE_IS_ACTIVE")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.ExerciseCreatedDate)
            .HasColumnName("EXERCISE_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.ExerciseUpdatedDate)
            .HasColumnName("EXERCISE_UPDATED_DATE")
            .IsRequired();

        // Lot 9 catalog columns (all nullable). The partial unique index on EXERCISE_SOURCE_CODE and
        // the CHECK on EXERCISE_MIN_LEVEL live in the Lot9_ExerciseCatalog migration as raw SQL.
        builder.Property(e => e.ExerciseSourceCode).HasColumnName("EXERCISE_SOURCE_CODE").HasMaxLength(10);
        builder.Property(e => e.ExerciseNameEn).HasColumnName("EXERCISE_NAME_EN").HasMaxLength(120);
        builder.Property(e => e.ExerciseCategory).HasColumnName("EXERCISE_CATEGORY").HasMaxLength(60);
        builder.Property(e => e.ExerciseMovementPattern).HasColumnName("EXERCISE_MOVEMENT_PATTERN").HasMaxLength(60);
        builder.Property(e => e.ExerciseType).HasColumnName("EXERCISE_TYPE").HasMaxLength(60);
        builder.Property(e => e.ExerciseLaterality).HasColumnName("EXERCISE_LATERALITY").HasMaxLength(40);
        builder.Property(e => e.ExerciseStartingPosition).HasColumnName("EXERCISE_STARTING_POSITION").HasMaxLength(80);
        builder.Property(e => e.ExerciseGripType).HasColumnName("EXERCISE_GRIP_TYPE").HasMaxLength(60);

        builder.Property(e => e.ExercisePrimaryMuscleGroupId).HasColumnName("EXERCISE_PRIMARY_MUSCLE_GROUP_ID");
        builder.Property(e => e.ExerciseSecondaryMuscles).HasColumnName("EXERCISE_SECONDARY_MUSCLES").HasColumnType("jsonb");
        builder.Property(e => e.ExercisePrimaryEquipmentId).HasColumnName("EXERCISE_PRIMARY_EQUIPMENT_ID");
        builder.Property(e => e.ExerciseSecondaryEquipmentId).HasColumnName("EXERCISE_SECONDARY_EQUIPMENT_ID");

        var minLevelConverter = new ValueConverter<ExerciseLevel, string>(
            v => ExerciseLevelToDb(v),
            v => ExerciseLevelFromDb(v));

        builder.Property(e => e.ExerciseMinLevel)
            .HasColumnName("EXERCISE_MIN_LEVEL")
            .HasMaxLength(20)
            .HasConversion(minLevelConverter);

        builder.Property(e => e.ExerciseBreathing).HasColumnName("EXERCISE_BREATHING").HasColumnType("text");
        builder.Property(e => e.ExerciseCommonMistakes).HasColumnName("EXERCISE_COMMON_MISTAKES").HasColumnType("jsonb");
        builder.Property(e => e.ExercisePrecautions).HasColumnName("EXERCISE_PRECAUTIONS").HasColumnType("text");
        builder.Property(e => e.ExerciseCoachTip).HasColumnName("EXERCISE_COACH_TIP").HasColumnType("text");
        builder.Property(e => e.ExerciseSearchTags).HasColumnName("EXERCISE_SEARCH_TAGS").HasColumnType("text");

        builder.HasIndex(e => e.ExercisePrimaryMuscleGroupId).HasDatabaseName("IX_EXERCISES_PRIMARY_MUSCLE_GROUP");
        builder.HasIndex(e => e.ExercisePrimaryEquipmentId).HasDatabaseName("IX_EXERCISES_PRIMARY_EQUIPMENT");
        builder.HasIndex(e => e.ExerciseSecondaryEquipmentId).HasDatabaseName("IX_EXERCISES_SECONDARY_EQUIPMENT");
        builder.HasIndex(e => e.ExerciseMinLevel).HasDatabaseName("IX_EXERCISES_MIN_LEVEL");

        // Restrict on all three: a reference row in use must never disappear, and SetNull would
        // silently erase business meaning (same reasoning as EXERCISE_COACH_ID below).
        builder.HasOne(e => e.PrimaryMuscleGroup)
            .WithMany()
            .HasForeignKey(e => e.ExercisePrimaryMuscleGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PrimaryEquipment)
            .WithMany()
            .HasForeignKey(e => e.ExercisePrimaryEquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.SecondaryEquipment)
            .WithMany()
            .HasForeignKey(e => e.ExerciseSecondaryEquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique index on (EXERCISE_COACH_ID, EXERCISE_NAME) with NULLS NOT DISTINCT is created
        // via raw SQL in the migration — HasIndex().IsUnique() cannot express NULLS NOT DISTINCT
        // and would otherwise generate a conflicting, weaker index (Postgres treats NULLs as
        // distinct by default, so two Mentora entries sharing a name would slip through).

        // Coach catalogue view: a coach's exercises filtered by active flag
        builder.HasIndex(e => new { e.ExerciseCoachId, e.ExerciseIsActive })
            .HasDatabaseName("IX_EXERCISES_COACH_ACTIVE");

        // Restrict, not Cascade or SetNull: Lot 6.3's PROGRAM_EXERCISES will hold an FK to
        // EXERCISES, so a coach delete must not cascade into rows referenced by active member
        // programs. SetNull is also wrong — EXERCISE_COACH_ID = NULL means "Mentora catalogue
        // entry, visible to everyone", so it would publish a deleted coach's private exercises.
        builder.HasOne(e => e.Coach)
            .WithMany(c => c.Exercises)
            .HasForeignKey(e => e.ExerciseCoachId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string ExerciseLevelToDb(ExerciseLevel v)
    {
        if (v == ExerciseLevel.Beginner)     return "BEGINNER";
        if (v == ExerciseLevel.Intermediate) return "INTERMEDIATE";
        if (v == ExerciseLevel.Advanced)     return "ADVANCED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ExerciseLevel value.");
    }

    private static ExerciseLevel ExerciseLevelFromDb(string v)
    {
        if (v == "BEGINNER")     return ExerciseLevel.Beginner;
        if (v == "INTERMEDIATE") return ExerciseLevel.Intermediate;
        if (v == "ADVANCED")     return ExerciseLevel.Advanced;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ExerciseLevel DB value.");
    }

    private static string MuscleGroupToDb(MuscleGroup v)
    {
        if (v == MuscleGroup.Chest)      return "CHEST";
        if (v == MuscleGroup.Back)       return "BACK";
        if (v == MuscleGroup.Shoulders)  return "SHOULDERS";
        if (v == MuscleGroup.Biceps)     return "BICEPS";
        if (v == MuscleGroup.Triceps)    return "TRICEPS";
        if (v == MuscleGroup.Forearms)   return "FOREARMS";
        if (v == MuscleGroup.Abs)        return "ABS";
        if (v == MuscleGroup.Quadriceps) return "QUADRICEPS";
        if (v == MuscleGroup.Hamstrings) return "HAMSTRINGS";
        if (v == MuscleGroup.Glutes)     return "GLUTES";
        if (v == MuscleGroup.Calves)     return "CALVES";
        if (v == MuscleGroup.FullBody)   return "FULL_BODY";
        if (v == MuscleGroup.Cardio)     return "CARDIO";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown MuscleGroup value.");
    }

    private static MuscleGroup MuscleGroupFromDb(string v)
    {
        if (v == "CHEST")      return MuscleGroup.Chest;
        if (v == "BACK")       return MuscleGroup.Back;
        if (v == "SHOULDERS")  return MuscleGroup.Shoulders;
        if (v == "BICEPS")     return MuscleGroup.Biceps;
        if (v == "TRICEPS")    return MuscleGroup.Triceps;
        if (v == "FOREARMS")   return MuscleGroup.Forearms;
        if (v == "ABS")        return MuscleGroup.Abs;
        if (v == "QUADRICEPS") return MuscleGroup.Quadriceps;
        if (v == "HAMSTRINGS") return MuscleGroup.Hamstrings;
        if (v == "GLUTES")     return MuscleGroup.Glutes;
        if (v == "CALVES")     return MuscleGroup.Calves;
        if (v == "FULL_BODY")  return MuscleGroup.FullBody;
        if (v == "CARDIO")     return MuscleGroup.Cardio;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown MuscleGroup DB value.");
    }

    private static string EquipmentToDb(Equipment v)
    {
        if (v == Equipment.Bodyweight) return "BODYWEIGHT";
        if (v == Equipment.Barbell)    return "BARBELL";
        if (v == Equipment.Dumbbell)   return "DUMBBELL";
        if (v == Equipment.Kettlebell) return "KETTLEBELL";
        if (v == Equipment.Machine)    return "MACHINE";
        if (v == Equipment.Cable)      return "CABLE";
        if (v == Equipment.Elastic)    return "ELASTIC";
        if (v == Equipment.Other)      return "OTHER";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown Equipment value.");
    }

    private static Equipment EquipmentFromDb(string v)
    {
        if (v == "BODYWEIGHT") return Equipment.Bodyweight;
        if (v == "BARBELL")    return Equipment.Barbell;
        if (v == "DUMBBELL")   return Equipment.Dumbbell;
        if (v == "KETTLEBELL") return Equipment.Kettlebell;
        if (v == "MACHINE")    return Equipment.Machine;
        if (v == "CABLE")      return Equipment.Cable;
        if (v == "ELASTIC")    return Equipment.Elastic;
        if (v == "OTHER")      return Equipment.Other;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown Equipment DB value.");
    }
}
