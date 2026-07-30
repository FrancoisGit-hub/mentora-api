using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

/// <summary>
/// A push-notification device registration for a user. USER_DEVICE_TOKEN carries a unique index:
/// the same physical device token handed to a different user must RE-ASSIGN this row to the new
/// USER_ID rather than create a duplicate row. The upsert logic that enforces this belongs to the
/// service layer (not implemented at this schema-only step).
/// </summary>
public class UserDevice
{
    public Guid UserDeviceId { get; set; }
    public string UserDeviceToken { get; set; } = null!;
    public DevicePlatform UserDevicePlatform { get; set; }
    public DateTime UserDeviceCreatedDate { get; set; }
    public DateTime UserDeviceLastSeenDate { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
