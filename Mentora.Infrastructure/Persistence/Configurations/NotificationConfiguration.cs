using Mentora.Core.Entities;
using Mentora.Core.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

/// <summary>
/// Lot 14.9b: NOTIFICATIONS. CK_NOTIFICATIONS_TYPE, CK_NOTIFICATIONS_STATUS, CK_NOTIFICATIONS_ATTEMPT_COUNT and
/// CK_NOTIFICATIONS_DATA are created by the migration (migrationBuilder.Sql), not declared here.
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("NOTIFICATIONS");

        builder.HasKey(e => e.NotificationId);
        builder.Property(e => e.NotificationId)
            .HasColumnName("NOTIFICATION_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.NotificationUserId).HasColumnName("NOTIFICATION_USER_ID").IsRequired();

        builder.Property(e => e.NotificationType)
            .HasColumnName("NOTIFICATION_TYPE").IsRequired().HasMaxLength(40);

        builder.Property(e => e.NotificationTitle)
            .HasColumnName("NOTIFICATION_TITLE").IsRequired().HasMaxLength(NotificationTexts.TitleMaxLength);

        builder.Property(e => e.NotificationBody)
            .HasColumnName("NOTIFICATION_BODY").IsRequired().HasMaxLength(NotificationTexts.BodyMaxLength);

        builder.Property(e => e.NotificationData)
            .HasColumnName("NOTIFICATION_DATA").HasColumnType("jsonb").IsRequired();

        builder.Property(e => e.NotificationStatus)
            .HasColumnName("NOTIFICATION_STATUS").IsRequired().HasMaxLength(20)
            .HasDefaultValue(NotificationStatuses.Pending);

        builder.Property(e => e.NotificationAttemptCount)
            .HasColumnName("NOTIFICATION_ATTEMPT_COUNT").IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.NotificationNextAttemptDate)
            .HasColumnName("NOTIFICATION_NEXT_ATTEMPT_DATE").IsRequired();

        builder.Property(e => e.NotificationSentDate).HasColumnName("NOTIFICATION_SENT_DATE");

        builder.Property(e => e.NotificationError).HasColumnName("NOTIFICATION_ERROR").HasMaxLength(40);

        builder.Property(e => e.NotificationClickedDate).HasColumnName("NOTIFICATION_CLICKED_DATE");

        builder.Property(e => e.NotificationCreatedDate).HasColumnName("NOTIFICATION_CREATED_DATE").IsRequired();
        builder.Property(e => e.NotificationUpdatedDate).HasColumnName("NOTIFICATION_UPDATED_DATE").IsRequired();

        // Restrict: a user who has notifications can never be deleted by accident.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.NotificationUserId)
            .HasConstraintName("FK_NOTIFICATIONS_USERS")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.NotificationStatus, e.NotificationNextAttemptDate })
            .HasDatabaseName("IX_NOTIFICATIONS_STATUS_NEXT_ATTEMPT");

        builder.HasIndex(e => e.NotificationUserId)
            .HasDatabaseName("IX_NOTIFICATIONS_USER_ID");
    }
}
