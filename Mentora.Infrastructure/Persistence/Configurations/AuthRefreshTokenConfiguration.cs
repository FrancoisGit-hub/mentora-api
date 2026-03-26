using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class AuthRefreshTokenConfiguration : IEntityTypeConfiguration<AuthRefreshToken>
{
    public void Configure(EntityTypeBuilder<AuthRefreshToken> builder)
    {
        builder.ToTable("AUTH_REFRESH_TOKENS");

        builder.HasKey(e => e.AuthRefreshTokenId);
        builder.Property(e => e.AuthRefreshTokenId)
            .HasColumnName("AUTH_REFRESH_TOKEN_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.AuthRefreshTokenHash).HasColumnName("AUTH_REFRESH_TOKEN_HASH").IsRequired().HasMaxLength(255);
        builder.Property(e => e.AuthRefreshTokenExpirationDate).HasColumnName("AUTH_REFRESH_TOKEN_EXPIRATION_DATE").IsRequired();
        builder.Property(e => e.AuthRefreshTokenIsRevoked).HasColumnName("AUTH_REFRESH_TOKEN_IS_REVOKED").IsRequired();
        builder.Property(e => e.AuthRefreshTokenCreatedDate).HasColumnName("AUTH_REFRESH_TOKEN_CREATED_DATE").IsRequired();
        builder.Property(e => e.AuthRefreshTokenRevokedDate).HasColumnName("AUTH_REFRESH_TOKEN_REVOKED_DATE");
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();
    }
}
