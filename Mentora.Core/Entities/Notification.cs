using Mentora.Core.Notifications;

namespace Mentora.Core.Entities;

/// <summary>
/// Lot 14.9b: one push notification, written in the same save as the business action (transactional outbox)
/// and sent by PushNotificationSender. NotificationType and NotificationStatus are strings
/// (see <see cref="NotificationTypes"/> and <see cref="NotificationStatuses"/>), checked by the database.
/// </summary>
public class Notification
{
    public Guid NotificationId { get; set; }

    /// <summary>Recipient user (USERS.USER_ID).</summary>
    public Guid NotificationUserId { get; set; }

    public string NotificationType { get; set; } = null!;
    public string NotificationTitle { get; set; } = null!;
    public string NotificationBody { get; set; } = null!;

    /// <summary>jsonb, an object of strings, without notificationId (added at send time).</summary>
    public string NotificationData { get; set; } = null!;

    // C# initialisers MUST match the database defaults ('PENDING', 0).
    public string NotificationStatus { get; set; } = NotificationStatuses.Pending;
    public int NotificationAttemptCount { get; set; } = 0;
    public DateTime NotificationNextAttemptDate { get; set; }
    public DateTime? NotificationSentDate { get; set; }

    /// <summary>Short code only (EXPIRED, NOT_CONFIGURED, ALL_TOKENS_INVALID, HTTP_xxx, RETRIES_EXHAUSTED). Never a token or a raw response.</summary>
    public string? NotificationError { get; set; }

    /// <summary>Written later by 14.9c (click route).</summary>
    public DateTime? NotificationClickedDate { get; set; }

    public DateTime NotificationCreatedDate { get; set; }
    public DateTime NotificationUpdatedDate { get; set; }
}
