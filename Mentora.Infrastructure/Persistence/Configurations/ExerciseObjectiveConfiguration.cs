using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ExerciseObjectiveConfiguration : IEntityTypeConfiguration<ExerciseObjective>
{
    public void Configure(EntityTypeBuilder<ExerciseObjective> builder)
    {
        builder.ToTable("EXERCISE_OBJECTIVES");

        builder.HasKey(e => new { e.ExerciseObjectiveExerciseId, e.ExerciseObjectiveObjectiveId });

        builder.Property(e => e.ExerciseObjectiveExerciseId)
            .HasColumnName("EXERCISE_OBJECTIVE_EXERCISE_ID");

        builder.Property(e => e.ExerciseObjectiveObjectiveId)
            .HasColumnName("EXERCISE_OBJECTIVE_OBJECTIVE_ID");

        // Cascade from the exercise (link rows are meaningless without it); Restrict from the
        // objective (a referenced objective must not silently disappear).
        builder.HasOne(e => e.Exercise)
            .WithMany(x => x.ExerciseObjectives)
            .HasForeignKey(e => e.ExerciseObjectiveExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Objective)
            .WithMany()
            .HasForeignKey(e => e.ExerciseObjectiveObjectiveId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ExerciseObjectiveObjectiveId)
            .HasDatabaseName("IX_EXERCISE_OBJECTIVES_OBJECTIVE");
    }
}
