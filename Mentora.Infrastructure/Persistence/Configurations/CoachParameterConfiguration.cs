using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class CoachParameterConfiguration : IEntityTypeConfiguration<CoachParameter>
{
    public void Configure(EntityTypeBuilder<CoachParameter> builder)
    {
        builder.ToTable("COACH_PARAMETERS");

        builder.HasKey(e => e.CoachParameterId);
        builder.Property(e => e.CoachParameterId)
            .HasColumnName("COACH_PARAMETER_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.CoachParameterHourlyRateEuros)
            .HasColumnName("COACH_PARAMETER_HOURLY_RATE_EUROS")
            .IsRequired()
            .HasColumnType("numeric(8,2)")
            .HasDefaultValue(50.00m);

        builder.Property(e => e.CoachParameterCancellationDelayHours)
            .HasColumnName("COACH_PARAMETER_CANCELLATION_DELAY_HOURS")
            .IsRequired()
            .HasDefaultValue(24);

        builder.Property(e => e.CoachParameterCreatedDate)
            .HasColumnName("COACH_PARAMETER_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.CoachParameterUpdatedDate)
            .HasColumnName("COACH_PARAMETER_UPDATED_DATE")
            .IsRequired();

        builder.Property(e => e.CoachId)
            .HasColumnName("COACH_ID")
            .IsRequired();

        builder.HasIndex(e => e.CoachId).IsUnique();

        builder.HasOne(e => e.Coach)
            .WithOne(c => c.CoachParameter)
            .HasForeignKey<CoachParameter>(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
