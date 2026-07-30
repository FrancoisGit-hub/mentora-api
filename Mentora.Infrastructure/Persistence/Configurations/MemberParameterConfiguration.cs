using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class MemberParameterConfiguration : IEntityTypeConfiguration<MemberParameter>
{
    public void Configure(EntityTypeBuilder<MemberParameter> builder)
    {
        builder.ToTable("MEMBER_PARAMETERS");

        builder.HasKey(e => e.MemberParameterId);
        builder.Property(e => e.MemberParameterId)
            .HasColumnName("MEMBER_PARAMETER_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.MemberParameterLanguage)
            .HasColumnName("MEMBER_PARAMETER_LANGUAGE")
            .IsRequired()
            .HasMaxLength(2)
            .HasDefaultValue("FR");

        builder.Property(e => e.MemberParameterNotifMessages)
            .HasColumnName("MEMBER_PARAMETER_NOTIF_MESSAGES")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.MemberParameterNotifSessionReminders)
            .HasColumnName("MEMBER_PARAMETER_NOTIF_SESSION_REMINDERS")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.MemberParameterNotifMarketing)
            .HasColumnName("MEMBER_PARAMETER_NOTIF_MARKETING")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.MemberParameterSessionReminderHoursBefore)
            .HasColumnName("MEMBER_PARAMETER_SESSION_REMINDER_HOURS_BEFORE")
            .IsRequired()
            .HasDefaultValue(24);

        builder.Property(e => e.MemberParameterCreatedDate)
            .HasColumnName("MEMBER_PARAMETER_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.MemberParameterUpdatedDate)
            .HasColumnName("MEMBER_PARAMETER_UPDATED_DATE")
            .IsRequired();

        builder.Property(e => e.MemberId)
            .HasColumnName("MEMBER_ID")
            .IsRequired();

        builder.HasIndex(e => e.MemberId).IsUnique();

        builder.HasOne(e => e.Member)
            .WithOne(m => m.MemberParameter)
            .HasForeignKey<MemberParameter>(e => e.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
