using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class SessionTemplateConfiguration : IEntityTypeConfiguration<SessionTemplate>
{
    public void Configure(EntityTypeBuilder<SessionTemplate> builder)
    {
        builder.ToTable("SESSION_TEMPLATES");

        builder.HasKey(e => e.SessionTemplateId);
        builder.Property(e => e.SessionTemplateId)
            .HasColumnName("SESSION_TEMPLATE_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        // NULL = Mentora platform template, set = the coach's own template.
        builder.Property(e => e.SessionTemplateCoachId)
            .HasColumnName("COACH_ID");

        builder.Property(e => e.SessionTemplateSourceCode)
            .HasColumnName("SESSION_TEMPLATE_SOURCE_CODE")
            .HasMaxLength(50);

        builder.Property(e => e.SessionTemplateName)
            .HasColumnName("SESSION_TEMPLATE_NAME")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.SessionTemplateDescription)
            .HasColumnName("SESSION_TEMPLATE_DESCRIPTION")
            .HasMaxLength(1000);

        // Plain strings restricted by named CHECKs (same wire values as PROGRAM_SESSIONS, no C# enum).
        builder.Property(e => e.SessionTemplateNature)
            .HasColumnName("SESSION_TEMPLATE_NATURE")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.SessionTemplateSessionType)
            .HasColumnName("SESSION_TEMPLATE_SESSION_TYPE")
            .IsRequired()
            .HasMaxLength(40);

        // Circuits of the session as a bare jsonb array, mapped as a string (never OwnsMany().ToJson()).
        builder.Property(e => e.SessionTemplateContent)
            .HasColumnName("SESSION_TEMPLATE_CONTENT")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.SessionTemplateIsActive)
            .HasColumnName("SESSION_TEMPLATE_IS_ACTIVE")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.SessionTemplateCreatedDate)
            .HasColumnName("SESSION_TEMPLATE_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.SessionTemplateUpdatedDate)
            .HasColumnName("SESSION_TEMPLATE_UPDATED_DATE")
            .IsRequired();

        // Restrict, never SetNull: COACH_ID NULL means "platform template visible to everyone", so
        // SetNull would publish a deleted coach's private templates.
        builder.HasOne(e => e.Coach)
            .WithMany()
            .HasForeignKey(e => e.SessionTemplateCoachId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.SessionTemplateCoachId)
            .HasDatabaseName("IX_SESSION_TEMPLATES_COACH_ID");

        // Not declared here, created by raw SQL in the migration Lot11_5_SessionTemplates:
        //  - UX_SESSION_TEMPLATES_SOURCE_CODE: partial unique index on SOURCE_CODE WHERE COACH_ID IS NULL
        //    (idempotence key of the 11.6 seeder; HasIndex cannot express the filter without a conflicting index);
        //  - CK_SESSION_TEMPLATES_SOURCE_CODE / _NATURE / _SESSION_TYPE / _CONTENT.
    }
}
