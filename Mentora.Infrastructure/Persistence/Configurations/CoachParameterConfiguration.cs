using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

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

        builder.Property(e => e.CoachCreditValueEuros).HasColumnName("COACH_CREDIT_VALUE_EUROS").IsRequired().HasDefaultValue(6.00m);
        builder.Property(e => e.CoachCancellationDelayHours).HasColumnName("COACH_CANCELLATION_DELAY_HOURS").IsRequired().HasDefaultValue(24);
        builder.Property(e => e.CoachParameterCreatedDate).HasColumnName("COACH_PARAMETER_CREATED_DATE").IsRequired();
        builder.Property(e => e.CoachParameterUpdatedDate).HasColumnName("COACH_PARAMETER_UPDATED_DATE").IsRequired();
        builder.Property(e => e.CoachId).HasColumnName("COACH_ID").IsRequired();

        builder.HasIndex(e => e.CoachId).IsUnique();

        builder.HasOne(e => e.Coach)
            .WithOne(c => c.CoachParameter)
            .HasForeignKey<CoachParameter>(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
