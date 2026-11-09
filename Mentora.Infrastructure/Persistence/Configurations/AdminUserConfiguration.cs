using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

/// <summary>
/// Lot 12.1 — ADMIN_USERS. CK_ADMIN_USERS_EMAIL_LOWERCASE is created by the migration
/// (migrationBuilder.Sql), not declared here.
/// </summary>
public class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.ToTable("ADMIN_USERS");

        builder.HasKey(e => e.AdminUserId);
        builder.Property(e => e.AdminUserId)
            .HasColumnName("ADMIN_USER_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.AdminUserEmail).HasColumnName("ADMIN_USER_EMAIL").IsRequired().HasMaxLength(255);
        builder.Property(e => e.AdminUserIsEnabled).HasColumnName("ADMIN_USER_IS_ENABLED").IsRequired();
        builder.Property(e => e.AdminUserLastLoginDate).HasColumnName("ADMIN_USER_LAST_LOGIN_DATE");
        builder.Property(e => e.AdminUserCreatedDate).HasColumnName("ADMIN_USER_CREATED_DATE").IsRequired();
        builder.Property(e => e.AdminUserUpdatedDate).HasColumnName("ADMIN_USER_UPDATED_DATE").IsRequired();

        builder.HasIndex(e => e.AdminUserEmail)
            .IsUnique()
            .HasDatabaseName("UX_ADMIN_USERS_EMAIL");

        builder.HasMany(e => e.AdminOtps)
            .WithOne(o => o.AdminUser)
            .HasForeignKey(o => o.AdminOtpAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.AdminRefreshTokens)
            .WithOne(t => t.AdminUser)
            .HasForeignKey(t => t.AdminRefreshTokenAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
