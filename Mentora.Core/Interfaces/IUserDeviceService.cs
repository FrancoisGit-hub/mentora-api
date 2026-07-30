using Mentora.Core.DTOs.Device;

namespace Mentora.Core.Interfaces;

public interface IUserDeviceService
{
    /// <summary>
    /// Registers a push-notification device token for the given user (upsert, keyed on the
    /// unique USER_DEVICE_TOKEN): inserts if unseen, refreshes LastSeenDate if it already
    /// belongs to this user, or REASSIGNS the row to this user if it belonged to someone else —
    /// a device handed to a different person must never keep delivering the previous owner's
    /// notifications, and the same token never produces a second row.
    /// </summary>
    Task RegisterAsync(Guid userId, RegisterDeviceRequest request, CancellationToken ct);

    /// <summary>
    /// Deletes the device row for the given token, but only if it belongs to the given user.
    /// A no-op (still succeeds) if the token doesn't exist or belongs to someone else.
    /// </summary>
    Task DeleteAsync(Guid userId, string token, CancellationToken ct);
}
