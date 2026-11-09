using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

/// <summary>Lot 12.1 — ADMIN_REFRESH_TOKENS. FK declared in AdminUserConfiguration.</summary>
public class AdminRefreshTokenConfiguration : IEntityTypeConfiguration<AdminRefreshToken>
{
    public void Configure(EntityTypeBuilder<AdminRefreshToken> builder)
    {
        builder.ToTable("ADMIN_REFRESH_TOKENS");

        builder.HasKey(e => e.AdminRefreshTokenId);
        builder.Property(e => e.AdminRefreshTokenId)
            .HasColumnName("ADMIN_REFRESH_TOKEN_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.AdminRefreshTokenAdminUserId).HasColumnName("ADMIN_REFRESH_TOKEN_ADMIN_USER_ID").IsRequired();
        builder.Property(e => e.AdminRefreshTokenHash).HasColumnName("ADMIN_REFRESH_TOKEN_HASH").IsRequired().HasMaxLength(255);
        builder.Property(e => e.AdminRefreshTokenExpirationDate).HasColumnName("ADMIN_REFRESH_TOKEN_EXPIRATION_DATE").IsRequired();
        builder.Property(e => e.AdminRefreshTokenIsRevoked).HasColumnName("ADMIN_REFRESH_TOKEN_IS_REVOKED").IsRequired();
        builder.Property(e => e.AdminRefreshTokenRevokedDate).HasColumnName("ADMIN_REFRESH_TOKEN_REVOKED_DATE");
        builder.Property(e => e.AdminRefreshTokenCreatedDate).HasColumnName("ADMIN_REFRESH_TOKEN_CREATED_DATE").IsRequired();

        builder.HasIndex(e => e.AdminRefreshTokenAdminUserId)
            .HasDatabaseName("IX_ADMIN_REFRESH_TOKENS_ADMIN_USER");
    }
}
