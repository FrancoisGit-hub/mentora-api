using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class AuthOtpConfiguration : IEntityTypeConfiguration<AuthOtp>
{
    public void Configure(EntityTypeBuilder<AuthOtp> builder)
    {
        builder.ToTable("AUTH_OTPS");

        builder.HasKey(e => e.AuthOtpId);
        builder.Property(e => e.AuthOtpId)
            .HasColumnName("AUTH_OTP_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.AuthOtpCodeHash).HasColumnName("AUTH_OTP_CODE_HASH").IsRequired().HasMaxLength(255);
        builder.Property(e => e.AuthOtpExpirationDate).HasColumnName("AUTH_OTP_EXPIRATION_DATE").IsRequired();
        builder.Property(e => e.AuthOtpIsUsed).HasColumnName("AUTH_OTP_IS_USED").IsRequired();
        builder.Property(e => e.AuthOtpCreatedDate).HasColumnName("AUTH_OTP_CREATED_DATE").IsRequired();
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();
    }
}
