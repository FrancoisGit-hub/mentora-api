using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable("USER_DEVICES");

        builder.HasKey(e => e.UserDeviceId);
        builder.Property(e => e.UserDeviceId)
            .HasColumnName("USER_DEVICE_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.UserDeviceToken)
            .HasColumnName("USER_DEVICE_TOKEN")
            .IsRequired()
            .HasMaxLength(512);

        builder.HasIndex(e => e.UserDeviceToken).IsUnique();

        // DevicePlatform — IOS / ANDROID (same pattern as SessionSlotConfiguration.OfferType)
        var platformConverter = new ValueConverter<DevicePlatform, string>(
            v => DevicePlatformToDb(v),
            v => DevicePlatformFromDb(v)
        );

        builder.Property(e => e.UserDevicePlatform)
            .HasColumnName("USER_DEVICE_PLATFORM")
            .IsRequired()
            .HasMaxLength(10)
            .HasConversion(platformConverter);

        builder.Property(e => e.UserDeviceCreatedDate)
            .HasColumnName("USER_DEVICE_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.UserDeviceLastSeenDate)
            .HasColumnName("USER_DEVICE_LAST_SEEN_DATE")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("USER_ID")
            .IsRequired();

        builder.HasOne(e => e.User)
            .WithMany(u => u.UserDevices)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static string DevicePlatformToDb(DevicePlatform v)
    {
        if (v == DevicePlatform.Ios)     return "IOS";
        if (v == DevicePlatform.Android) return "ANDROID";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown DevicePlatform value.");
    }

    private static DevicePlatform DevicePlatformFromDb(string v)
    {
        if (v == "IOS")     return DevicePlatform.Ios;
        if (v == "ANDROID") return DevicePlatform.Android;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown DevicePlatform DB value.");
    }
}
