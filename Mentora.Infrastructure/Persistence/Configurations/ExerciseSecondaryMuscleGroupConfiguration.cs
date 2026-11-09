using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

/// <summary>Lot 12.2a — EXERCISE_SECONDARY_MUSCLE_GROUPS (composite PK: exercise + muscle group).</summary>
public class ExerciseSecondaryMuscleGroupConfiguration : IEntityTypeConfiguration<ExerciseSecondaryMuscleGroup>
{
    public void Configure(EntityTypeBuilder<ExerciseSecondaryMuscleGroup> builder)
    {
        builder.ToTable("EXERCISE_SECONDARY_MUSCLE_GROUPS");

        builder.HasKey(e => new { e.ExerciseSecondaryMuscleGroupExerciseId, e.ExerciseSecondaryMuscleGroupMuscleGroupId });

        builder.Property(e => e.ExerciseSecondaryMuscleGroupExerciseId)
            .HasColumnName("EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID");

        builder.Property(e => e.ExerciseSecondaryMuscleGroupMuscleGroupId)
            .HasColumnName("EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID");

        // Same rule as EXERCISE_OBJECTIVES: Cascade from the exercise, Restrict from the reference row.
        // Explicit FK names: the EF-generated ones would exceed the 63-character PostgreSQL limit.
        builder.HasOne(e => e.Exercise)
            .WithMany(x => x.SecondaryMuscleGroups)
            .HasForeignKey(e => e.ExerciseSecondaryMuscleGroupExerciseId)
            .HasConstraintName("FK_EXERCISE_SECONDARY_MUSCLE_GROUPS_EXERCISE")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.MuscleGroup)
            .WithMany()
            .HasForeignKey(e => e.ExerciseSecondaryMuscleGroupMuscleGroupId)
            .HasConstraintName("FK_EXERCISE_SECONDARY_MUSCLE_GROUPS_MUSCLE_GROUP")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ExerciseSecondaryMuscleGroupMuscleGroupId)
            .HasDatabaseName("IX_EXERCISE_SECONDARY_MUSCLE_GROUPS_MUSCLE_GROUP");
    }
}
