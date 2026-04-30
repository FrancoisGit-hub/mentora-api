using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class OfferProgramConfiguration : IEntityTypeConfiguration<OfferProgram>
{
    public void Configure(EntityTypeBuilder<OfferProgram> builder)
    {
        builder.ToTable("OFFER_PROGRAMS");

        builder.HasKey(e => e.OfferProgramId);
        builder.Property(e => e.OfferProgramId)
            .HasColumnName("OFFER_PROGRAM_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.OfferProgramName)
            .HasColumnName("OFFER_PROGRAM_NAME")
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(e => e.OfferProgramIsActive)
            .HasColumnName("OFFER_PROGRAM_IS_ACTIVE")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.OfferProgramCreatedDate)
            .HasColumnName("OFFER_PROGRAM_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.CoachId)
            .HasColumnName("COACH_ID")
            .IsRequired();

        // Partial unique index: no two active programs with the same name per coach
        builder.HasIndex(e => new { e.CoachId, e.OfferProgramName })
            .IsUnique()
            .HasFilter("\"OFFER_PROGRAM_IS_ACTIVE\" = true")
            .HasDatabaseName("IX_OFFER_PROGRAMS_ACTIVE_NAME");

        builder.HasOne(e => e.Coach)
            .WithMany(c => c.OfferPrograms)
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
