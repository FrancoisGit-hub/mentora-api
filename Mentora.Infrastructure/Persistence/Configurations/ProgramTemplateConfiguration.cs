using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ProgramTemplateConfiguration : IEntityTypeConfiguration<ProgramTemplate>
{
    public void Configure(EntityTypeBuilder<ProgramTemplate> builder)
    {
        builder.ToTable("PROGRAM_TEMPLATES");

        builder.HasKey(e => e.ProgramTemplateId);
        builder.Property(e => e.ProgramTemplateId)
            .HasColumnName("PROGRAM_TEMPLATE_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        // NULL means "Mentora template" — no coach owns this row
        builder.Property(e => e.ProgramTemplateCoachId)
            .HasColumnName("PROGRAM_TEMPLATE_COACH_ID");

        builder.Property(e => e.ProgramTemplateName)
            .HasColumnName("PROGRAM_TEMPLATE_NAME")
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(e => e.ProgramTemplateDescription)
            .HasColumnName("PROGRAM_TEMPLATE_DESCRIPTION")
            .HasColumnType("text");

        var goalConverter = new ValueConverter<ProgramGoal, string>(
            v => ProgramGoalToDb(v),
            v => ProgramGoalFromDb(v));

        builder.Property(e => e.ProgramTemplateGoal)
            .HasColumnName("PROGRAM_TEMPLATE_GOAL")
            .IsRequired()
            .HasMaxLength(40)
            .HasConversion(goalConverter);

        builder.Property(e => e.ProgramTemplateDurationWeeks)
            .HasColumnName("PROGRAM_TEMPLATE_DURATION_WEEKS")
            .IsRequired();

        // Recursive block tree — cannot use OwnsOne/OwnsMany/ToJson (EF owned types cannot be
        // recursive). Plain jsonb string; the service owns serialization via System.Text.Json.
        builder.Property(e => e.ProgramTemplateBody)
            .HasColumnName("PROGRAM_TEMPLATE_BODY")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.ProgramTemplateIsActive)
            .HasColumnName("PROGRAM_TEMPLATE_IS_ACTIVE")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.ProgramTemplateCreatedDate)
            .HasColumnName("PROGRAM_TEMPLATE_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.ProgramTemplateUpdatedDate)
            .HasColumnName("PROGRAM_TEMPLATE_UPDATED_DATE")
            .IsRequired();

        // Unique index on (COACH_ID, NAME) with NULLS NOT DISTINCT is created via raw SQL in
        // the migration — HasIndex().IsUnique() cannot express NULLS NOT DISTINCT and would
        // otherwise generate a conflicting, weaker index (Postgres treats NULLs as distinct by
        // default, so two Mentora templates sharing a name would slip through).

        // Coach library view: a coach's templates filtered by active flag
        builder.HasIndex(e => new { e.ProgramTemplateCoachId, e.ProgramTemplateIsActive })
            .HasDatabaseName("IX_PROGRAM_TEMPLATES_COACH_ACTIVE");

        // Restrict, not Cascade or SetNull: Lot 6.3's PROGRAM_EXERCISES-equivalent will hold an
        // FK to PROGRAM_TEMPLATES, so a coach delete must not cascade into rows referenced by
        // active member programs. SetNull is also wrong — PROGRAM_TEMPLATE_COACH_ID = NULL means
        // "Mentora template, visible to everyone", so it would publish a deleted coach's private
        // templates. Same reasoning as EXERCISES (Lot 6.1).
        builder.HasOne(e => e.Coach)
            .WithMany(c => c.ProgramTemplates)
            .HasForeignKey(e => e.ProgramTemplateCoachId)
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
}
