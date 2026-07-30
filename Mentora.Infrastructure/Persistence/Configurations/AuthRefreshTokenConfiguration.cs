using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Mentora.Core.Entities;
using Mentora.Core.Enums;

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

        // UserRole — MEMBER / COACH (same pattern as SessionSlotConfiguration.OfferType)
        var userRoleConverter = new ValueConverter<UserRole, string>(
            v => UserRoleToDb(v),
            v => UserRoleFromDb(v)
        );

        builder.Property(e => e.AuthRefreshTokenUserRole)
            .HasColumnName("AUTH_REFRESH_TOKEN_USER_ROLE")
            .IsRequired()
            .HasMaxLength(10)
            .HasConversion(userRoleConverter);

        builder.Property(e => e.AuthRefreshTokenCreatedDate).HasColumnName("AUTH_REFRESH_TOKEN_CREATED_DATE").IsRequired();
        builder.Property(e => e.AuthRefreshTokenRevokedDate).HasColumnName("AUTH_REFRESH_TOKEN_REVOKED_DATE");
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();
    }

    private static string UserRoleToDb(UserRole v)
    {
        if (v == UserRole.Member) return "MEMBER";
        if (v == UserRole.Coach)  return "COACH";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown UserRole value.");
    }

    private static UserRole UserRoleFromDb(string v)
    {
        if (v == "MEMBER") return UserRole.Member;
        if (v == "COACH")  return UserRole.Coach;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown UserRole DB value.");
    }
}
