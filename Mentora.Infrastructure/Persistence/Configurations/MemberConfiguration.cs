using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Mentora.Core.Entities;
using Mentora.Core.Enums;

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

        // Gender — MALE / FEMALE / OTHER (same pattern as SessionSlotConfiguration.OfferType)
        var genderConverter = new ValueConverter<Gender, string>(
            v => GenderToDb(v),
            v => GenderFromDb(v)
        );

        builder.Property(e => e.MemberGender)
            .HasColumnName("MEMBER_GENDER")
            .HasMaxLength(10)
            .HasConversion(genderConverter);

        builder.Property(e => e.MemberHeightCm)
            .HasColumnName("MEMBER_HEIGHT_CM")
            .HasColumnType("smallint");

        builder.Property(e => e.MemberBirthDate)
            .HasColumnName("MEMBER_BIRTH_DATE")
            .HasColumnType("date");

        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();

        builder.HasMany(e => e.MemberCoaches)
            .WithOne(mc => mc.Member)
            .HasForeignKey(mc => mc.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static string GenderToDb(Gender v)
    {
        if (v == Gender.Male)   return "MALE";
        if (v == Gender.Female) return "FEMALE";
        if (v == Gender.Other)  return "OTHER";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown Gender value.");
    }

    private static Gender GenderFromDb(string v)
    {
        if (v == "MALE")   return Gender.Male;
        if (v == "FEMALE") return Gender.Female;
        if (v == "OTHER")  return Gender.Other;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown Gender DB value.");
    }
}
