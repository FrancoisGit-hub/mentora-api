using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

/// <summary>
/// Lot 12.1 — ADMIN_OTPS. CK_ADMIN_OTPS_FAILED_ATTEMPTS is created by the migration
/// (migrationBuilder.Sql), not declared here. FK declared in AdminUserConfiguration.
/// </summary>
public class AdminOtpConfiguration : IEntityTypeConfiguration<AdminOtp>
{
    public void Configure(EntityTypeBuilder<AdminOtp> builder)
    {
        builder.ToTable("ADMIN_OTPS");

        builder.HasKey(e => e.AdminOtpId);
        builder.Property(e => e.AdminOtpId)
            .HasColumnName("ADMIN_OTP_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.AdminOtpAdminUserId).HasColumnName("ADMIN_OTP_ADMIN_USER_ID").IsRequired();
        builder.Property(e => e.AdminOtpCodeHash).HasColumnName("ADMIN_OTP_CODE_HASH").IsRequired().HasMaxLength(255);
        builder.Property(e => e.AdminOtpExpirationDate).HasColumnName("ADMIN_OTP_EXPIRATION_DATE").IsRequired();
        builder.Property(e => e.AdminOtpIsUsed).HasColumnName("ADMIN_OTP_IS_USED").IsRequired();
        builder.Property(e => e.AdminOtpFailedAttempts).HasColumnName("ADMIN_OTP_FAILED_ATTEMPTS").IsRequired();
        builder.Property(e => e.AdminOtpCreatedDate).HasColumnName("ADMIN_OTP_CREATED_DATE").IsRequired();

        builder.HasIndex(e => e.AdminOtpAdminUserId)
            .HasDatabaseName("IX_ADMIN_OTPS_ADMIN_USER");
    }
}
