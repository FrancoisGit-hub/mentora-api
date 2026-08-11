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
