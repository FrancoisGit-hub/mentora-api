using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("MEMBERS");

        builder.HasKey(e => e.MemberId);
        builder.Property(e => e.MemberId)
            .HasColumnName("MEMBER_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.MemberFirstName).HasColumnName("MEMBER_FIRST_NAME").IsRequired().HasMaxLength(100);
        builder.Property(e => e.MemberLastName).HasColumnName("MEMBER_LAST_NAME").IsRequired().HasMaxLength(100);
        builder.Property(e => e.MemberPhone).HasColumnName("MEMBER_PHONE").HasMaxLength(20);
        builder.Property(e => e.MemberCreatedDate).HasColumnName("MEMBER_CREATED_DATE").IsRequired();
        builder.Property(e => e.MemberIsActive).HasColumnName("MEMBER_IS_ACTIVE").IsRequired();
        builder.Property(e => e.MemberHasActivated).HasColumnName("MEMBER_HAS_ACTIVATED").IsRequired();
        builder.Property(e => e.MemberActivationDate).HasColumnName("MEMBER_ACTIVATION_DATE");
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();

        builder.HasMany(e => e.MemberCoaches)
            .WithOne(mc => mc.Member)
            .HasForeignKey(mc => mc.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
