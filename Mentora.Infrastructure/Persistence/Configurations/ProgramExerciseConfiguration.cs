using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ProgramExerciseConfiguration : IEntityTypeConfiguration<ProgramExercise>
{
    public void Configure(EntityTypeBuilder<ProgramExercise> builder)
    {
        builder.ToTable("PROGRAM_EXERCISES");

        builder.HasKey(e => e.ProgramExerciseId);
        builder.Property(e => e.ProgramExerciseId)
            .HasColumnName("PROGRAM_EXERCISE_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        // Denormalized on purpose — see WHY PROGRAM_ID IS DENORMALIZED (ProgramConfiguration.cs)
        builder.Property(e => e.ProgramExerciseProgramId)
            .HasColumnName("PROGRAM_EXERCISE_PROGRAM_ID")
            .IsRequired();

        builder.Property(e => e.ProgramExerciseCircuitId)
            .HasColumnName("PROGRAM_EXERCISE_CIRCUIT_ID")
            .IsRequired();

        // Pointer, never copied — resolved separately, in one query, at read time
        builder.Property(e => e.ProgramExerciseExerciseId)
            .HasColumnName("PROGRAM_EXERCISE_EXERCISE_ID")
            .IsRequired();

        builder.Property(e => e.ProgramExercisePosition)
            .HasColumnName("PROGRAM_EXERCISE_POSITION")
            .IsRequired();

        builder.Property(e => e.ProgramExerciseLoadType)
            .HasColumnName("PROGRAM_EXERCISE_LOAD_TYPE")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.ProgramExerciseCustomNote)
            .HasColumnName("PROGRAM_EXERCISE_CUSTOM_NOTE")
            .HasColumnType("text");

        builder.Property(e => e.ProgramExercisePrescribedSets)
            .HasColumnName("PROGRAM_EXERCISE_PRESCRIBED_SETS");

        builder.Property(e => e.ProgramExercisePrescribedReps)
            .HasColumnName("PROGRAM_EXERCISE_PRESCRIBED_REPS");

        builder.Property(e => e.ProgramExercisePrescribedWeightKg)
            .HasColumnName("PROGRAM_EXERCISE_PRESCRIBED_WEIGHT_KG")
            .HasColumnType("numeric(6,2)");

        builder.Property(e => e.ProgramExerciseRestSeconds)
            .HasColumnName("PROGRAM_EXERCISE_REST_SECONDS");

        builder.Property(e => e.ProgramExerciseWorkSeconds)
            .HasColumnName("PROGRAM_EXERCISE_WORK_SECONDS");

        builder.Property(e => e.ProgramExerciseRestWorkSeconds)
            .HasColumnName("PROGRAM_EXERCISE_REST_WORK_SECONDS");

        // Stay unwritten until Lot 6.6 — the prescribed values must never be overwritten by actuals.
        builder.Property(e => e.ProgramExerciseActualSets)
            .HasColumnName("PROGRAM_EXERCISE_ACTUAL_SETS");

        builder.Property(e => e.ProgramExerciseActualReps)
            .HasColumnName("PROGRAM_EXERCISE_ACTUAL_REPS");

        builder.Property(e => e.ProgramExerciseActualWeightKg)
            .HasColumnName("PROGRAM_EXERCISE_ACTUAL_WEIGHT_KG")
            .HasColumnType("numeric(6,2)");

        builder.Property(e => e.ProgramExerciseActualRpe)
            .HasColumnName("PROGRAM_EXERCISE_ACTUAL_RPE");

        builder.Property(e => e.ProgramExerciseMemberFeedback)
            .HasColumnName("PROGRAM_EXERCISE_MEMBER_FEEDBACK")
            .HasColumnType("text");

        builder.HasOne<Program>()
            .WithMany()
            .HasForeignKey(e => e.ProgramExerciseProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ProgramCircuit>()
            .WithMany()
            .HasForeignKey(e => e.ProgramExerciseCircuitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Exercise>()
            .WithMany()
            .HasForeignKey(e => e.ProgramExerciseExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
