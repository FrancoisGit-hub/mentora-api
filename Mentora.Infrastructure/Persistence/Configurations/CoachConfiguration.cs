using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class CoachConfiguration : IEntityTypeConfiguration<Coach>
{
    public void Configure(EntityTypeBuilder<Coach> builder)
    {
        builder.ToTable("COACHES");

        builder.HasKey(e => e.CoachId);
        builder.Property(e => e.CoachId)
            .HasColumnName("COACH_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.CoachFirstName).HasColumnName("COACH_FIRST_NAME").IsRequired().HasMaxLength(100);
        builder.Property(e => e.CoachLastName).HasColumnName("COACH_LAST_NAME").IsRequired().HasMaxLength(100);
        builder.Property(e => e.CoachPhone).HasColumnName("COACH_PHONE").HasMaxLength(20);
        builder.Property(e => e.CoachCreatedDate).HasColumnName("COACH_CREATED_DATE").IsRequired();
        builder.Property(e => e.CoachIsActive).HasColumnName("COACH_IS_ACTIVE").IsRequired();
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();

        builder.HasOne(e => e.AgentToken)
            .WithOne(t => t.Coach)
            .HasForeignKey<AgentToken>(t => t.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
