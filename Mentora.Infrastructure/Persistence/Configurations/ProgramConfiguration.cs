using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

// WHY PROGRAM_ID IS DENORMALIZED ON PROGRAM_SESSIONS / PROGRAM_CIRCUITS / PROGRAM_EXERCISES
// (not just the immediate parent id): loading the tree with chained Include produces a
// cartesian blow-up. With PROGRAM_ID present on all four child tables, ProgramService loads the
// tree as five flat queries filtered on PROGRAM_ID and assembles it in memory. Rows are written
// once by the assignment copy and never re-parented, so the redundancy cannot drift.
public class ProgramConfiguration : IEntityTypeConfiguration<Program>
{
    public void Configure(EntityTypeBuilder<Program> builder)
    {
        builder.ToTable("PROGRAMS");

        builder.HasKey(e => e.ProgramId);
        builder.Property(e => e.ProgramId)
            .HasColumnName("PROGRAM_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.ProgramCoachId)
            .HasColumnName("PROGRAM_COACH_ID")
            .IsRequired();

        builder.Property(e => e.ProgramMemberId)
            .HasColumnName("PROGRAM_MEMBER_ID")
            .IsRequired();

        // Weak reference — no FK constraint; origin trace only, same pattern as
        // SessionVoucher.ProductId. Editing or soft-deleting the source template must have zero
        // effect on programs already copied from it.
        builder.Property(e => e.ProgramTemplateId)
            .HasColumnName("PROGRAM_TEMPLATE_ID");

        builder.Property(e => e.ProgramName)
            .HasColumnName("PROGRAM_NAME")
            .IsRequired()
            .HasMaxLength(120);

        var goalConverter = new ValueConverter<ProgramGoal, string>(
            v => ProgramGoalToDb(v),
            v => ProgramGoalFromDb(v));

        builder.Property(e => e.ProgramGoal)
            .HasColumnName("PROGRAM_GOAL")
            .IsRequired()
            .HasMaxLength(40)
            .HasConversion(goalConverter);

        builder.Property(e => e.ProgramStartDate)
            .HasColumnName("PROGRAM_START_DATE")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.ProgramDurationWeeks)
            .HasColumnName("PROGRAM_DURATION_WEEKS")
            .IsRequired();

        // Unused until Lot 6.5
        builder.Property(e => e.ProgramWeekOffset)
            .HasColumnName("PROGRAM_WEEK_OFFSET")
            .IsRequired()
            .HasDefaultValue(0);

        var statusConverter = new ValueConverter<ProgramStatus, string>(
            v => ProgramStatusToDb(v),
            v => ProgramStatusFromDb(v));

        builder.Property(e => e.ProgramStatus)
            .HasColumnName("PROGRAM_STATUS")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(statusConverter)
            .HasDefaultValueSql("'ACTIVE'");

        builder.Property(e => e.ProgramCreatedDate)
            .HasColumnName("PROGRAM_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.ProgramUpdatedDate)
            .HasColumnName("PROGRAM_UPDATED_DATE")
            .IsRequired();

        // Partial unique index (WHERE PROGRAM_STATUS = 'ACTIVE') is created via raw SQL in the
        // migration — not declared here, same treatment as the NULLS NOT DISTINCT indexes on
        // EXERCISES / PROGRAM_TEMPLATES: kept out of the EF-tracked model, explicit in the
        // migration file.

        builder.HasOne(e => e.Coach)
            .WithMany(c => c.Programs)
            .HasForeignKey(e => e.ProgramCoachId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Member)
            .WithMany(m => m.Programs)
            .HasForeignKey(e => e.ProgramMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string ProgramGoalToDb(ProgramGoal v)
    {
        if (v == ProgramGoal.MuscleGain)     return "MUSCLE_GAIN";
        if (v == ProgramGoal.FatLoss)        return "FAT_LOSS";
        if (v == ProgramGoal.Strength)       return "STRENGTH";
        if (v == ProgramGoal.Endurance)      return "ENDURANCE";
        if (v == ProgramGoal.Mobility)       return "MOBILITY";
        if (v == ProgramGoal.Rehab)          return "REHAB";
        if (v == ProgramGoal.GeneralFitness) return "GENERAL_FITNESS";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ProgramGoal value.");
    }

    private static ProgramGoal ProgramGoalFromDb(string v)
    {
        if (v == "MUSCLE_GAIN")     return ProgramGoal.MuscleGain;
        if (v == "FAT_LOSS")        return ProgramGoal.FatLoss;
        if (v == "STRENGTH")        return ProgramGoal.Strength;
        if (v == "ENDURANCE")       return ProgramGoal.Endurance;
        if (v == "MOBILITY")        return ProgramGoal.Mobility;
        if (v == "REHAB")           return ProgramGoal.Rehab;
        if (v == "GENERAL_FITNESS") return ProgramGoal.GeneralFitness;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ProgramGoal DB value.");
    }

    private static string ProgramStatusToDb(ProgramStatus v)
    {
        if (v == ProgramStatus.Active)    return "ACTIVE";
        if (v == ProgramStatus.Completed) return "COMPLETED";
        if (v == ProgramStatus.Archived)  return "ARCHIVED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ProgramStatus value.");
    }

    private static ProgramStatus ProgramStatusFromDb(string v)
    {
        if (v == "ACTIVE")    return ProgramStatus.Active;
        if (v == "COMPLETED") return ProgramStatus.Completed;
        if (v == "ARCHIVED")  return ProgramStatus.Archived;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ProgramStatus DB value.");
    }
}
