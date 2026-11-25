namespace Mentora.Core.Notifications;

/// <summary>Lot 14.9b: data.type values, also the values of NOTIFICATION_TYPE (CK_NOTIFICATIONS_TYPE). Exact strings.</summary>
public static class NotificationTypes
{
    public const string ChatMessage = "CHAT_MESSAGE";
    public const string SessionBooked = "SESSION_BOOKED";
    public const string SessionCancelled = "SESSION_CANCELLED";

    /// <summary>Allowed by the CHECK now so 14.9c needs no migration. Nothing emits it in 14.9b.</summary>
    public const string SessionReminder = "SESSION_REMINDER";

    public const string MemberJoined = "MEMBER_JOINED";
}

/// <summary>Lot 14.9b: values of NOTIFICATION_STATUS (CK_NOTIFICATIONS_STATUS).</summary>
public static class NotificationStatuses
{
    public const string Pending = "PENDING";
    public const string Sent = "SENT";
    public const string NoDevice = "NO_DEVICE";
    public const string Failed = "FAILED";
}

/// <summary>Lot 14.9b: short error codes stored in NOTIFICATION_ERROR (never a token or a raw response).</summary>
public static class NotificationErrors
{
    public const string Expired = "EXPIRED";
    public const string NotConfigured = "NOT_CONFIGURED";
    public const string AllTokensInvalid = "ALL_TOKENS_INVALID";
    public const string RetriesExhausted = "RETRIES_EXHAUSTED";
    public const string SendFailed = "SEND_FAILED";
    public const string Exception = "EXCEPTION";

    public static string Http(int code) => "HTTP_" + code;
}
