using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class MemberCoachConfiguration : IEntityTypeConfiguration<MemberCoach>
{
    public void Configure(EntityTypeBuilder<MemberCoach> builder)
    {
        builder.ToTable("MEMBER_COACHES");

        builder.HasKey(e => e.MemberCoachId);
        builder.Property(e => e.MemberCoachId)
            .HasColumnName("MEMBER_COACH_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.MemberId).HasColumnName("MEMBER_ID").IsRequired();
        builder.Property(e => e.CoachId).HasColumnName("COACH_ID").IsRequired();
        builder.Property(e => e.IsPrimary).HasColumnName("IS_PRIMARY").IsRequired().HasDefaultValue(false);
        builder.Property(e => e.StartedAt).HasColumnName("STARTED_AT").IsRequired().HasDefaultValueSql("now()");

        builder.Property(e => e.MemberCoachPresentialAddress)
            .HasColumnName("MEMBER_COACH_PRESENTIAL_ADDRESS")
            .HasColumnType("text");

        builder.HasIndex(e => new { e.MemberId, e.CoachId }).IsUnique();

        builder.HasOne(e => e.Member)
            .WithMany(m => m.MemberCoaches)
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Coach)
            .WithMany(c => c.MemberCoaches)
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
