using Mentora.Core.DTOs.Account;

namespace Mentora.Core.Interfaces;

public interface IAccountService
{
    /// <summary>
    /// Records that the calling user has requested account deletion. If a request is already
    /// pending, only the reason is refreshed — the original requested date is kept. Support
    /// processes the request manually; nothing is deleted by this call.
    /// </summary>
    Task RequestDeletionAsync(Guid userId, AccountDeletionRequestDto request, CancellationToken ct);

    /// <summary>Cancels a pending deletion request for the calling user (both columns back to null).</summary>
    Task CancelDeletionRequestAsync(Guid userId, CancellationToken ct);
}
