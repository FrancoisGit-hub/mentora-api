using Mentora.Core.DTOs.Admin.Auth;

namespace Mentora.Core.Interfaces;

/// <summary>Lot 12.1 — admin back-office login (ADMIN_USERS, never USERS).</summary>
public interface IAdminAuthService
{
    /// <summary>Sends a code when the email is a known enabled admin; returns normally in every case.</summary>
    Task RequestCodeAsync(AdminRequestCodeRequest request, CancellationToken ct);

    /// <summary>Any failure throws InvalidOperationException("Invalid or expired code.").</summary>
    Task<AdminAuthResponse> VerifyCodeAsync(AdminVerifyCodeRequest request, CancellationToken ct);

    /// <summary>Any failure throws UnauthorizedException("Invalid or expired refresh token.").</summary>
    Task<AdminAuthResponse> RefreshAsync(AdminRefreshRequest request, CancellationToken ct);

    /// <summary>Never throws for a missing, malformed, unknown or already revoked token.</summary>
    Task LogoutAsync(AdminLogoutRequest request, CancellationToken ct);

    /// <summary>Throws UnauthorizedException when the admin is unknown or disabled.</summary>
    Task<AdminMeResponse> GetMeAsync(Guid adminUserId, CancellationToken ct);
}
