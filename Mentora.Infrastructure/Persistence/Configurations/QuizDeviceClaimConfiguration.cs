using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class QuizDeviceClaimConfiguration : IEntityTypeConfiguration<QuizDeviceClaim>
{
    public void Configure(EntityTypeBuilder<QuizDeviceClaim> builder)
    {
        builder.ToTable("QUIZ_DEVICE_CLAIMS");

        builder.HasKey(c => c.QuizDeviceClaimId);
        builder.Property(c => c.QuizDeviceClaimId)
            .HasColumnName("QUIZ_DEVICE_CLAIM_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        // One claim per device, ever.
        builder.Property(c => c.QuizDeviceClaimDeviceId).HasColumnName("QUIZ_DEVICE_CLAIM_DEVICE_ID").IsRequired().HasMaxLength(64);
        builder.HasIndex(c => c.QuizDeviceClaimDeviceId).IsUnique().HasDatabaseName("UX_QUIZ_DEVICE_CLAIMS_DEVICE_ID");

        builder.Property(c => c.QuizDeviceClaimCoachId).HasColumnName("QUIZ_DEVICE_CLAIM_COACH_ID").IsRequired();
        builder.Property(c => c.QuizDeviceClaimPoints).HasColumnName("QUIZ_DEVICE_CLAIM_POINTS").IsRequired();
        builder.Property(c => c.QuizDeviceClaimCreatedDate).HasColumnName("QUIZ_DEVICE_CLAIM_CREATED_DATE").IsRequired();

        builder.HasIndex(c => c.QuizDeviceClaimCoachId).HasDatabaseName("IX_QUIZ_DEVICE_CLAIMS_COACH_ID");

        builder.HasOne(c => c.Coach)
            .WithMany()
            .HasForeignKey(c => c.QuizDeviceClaimCoachId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
