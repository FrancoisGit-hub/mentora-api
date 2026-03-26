using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("USERS");

        builder.HasKey(e => e.UserId);
        builder.Property(e => e.UserId)
            .HasColumnName("USER_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.UserCreatedDate).HasColumnName("USER_CREATED_DATE").IsRequired();
        builder.Property(e => e.UserModificationDate).HasColumnName("USER_MODIFICATION_DATE");
        builder.Property(e => e.UserEmail).HasColumnName("USER_EMAIL").IsRequired().HasMaxLength(255);
        builder.HasIndex(e => e.UserEmail).IsUnique();
        builder.Property(e => e.UserPsw).HasColumnName("USER_PSW").HasMaxLength(255);
        builder.Property(e => e.UserLogin).HasColumnName("USER_LOGIN").HasMaxLength(100);
        builder.Property(e => e.UserIsEnabled).HasColumnName("USER_IS_ENABLED").IsRequired();
        builder.Property(e => e.UserDisabledDate).HasColumnName("USER_DISABLED_DATE");
        builder.Property(e => e.UserRole).HasColumnName("USER_ROLE").IsRequired().HasMaxLength(50);

        builder.HasOne(e => e.Coach)
            .WithOne(c => c.User)
            .HasForeignKey<Coach>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Member)
            .WithOne(m => m.User)
            .HasForeignKey<Member>(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.AuthOtps)
            .WithOne(o => o.User)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.AuthRefreshTokens)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
