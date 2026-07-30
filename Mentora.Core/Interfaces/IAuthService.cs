using Mentora.Core.DTOs.Auth;

namespace Mentora.Core.Interfaces;

public interface IAuthService
{
    Task RequestOtpAsync(string email, bool? isCoach);
    Task<AuthResponse> VerifyOtpAsync(string email, string code, bool? isCoach);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Revokes the given refresh token (if it belongs to <paramref name="userId"/>) and, if a
    /// device token is supplied, deletes that device's push-notification registration for this
    /// user. Never throws for an unknown, already-revoked, expired, or someone-else's token —
    /// always succeeds, so the response never reveals whether the token existed.
    /// </summary>
    /// <remarks>
    /// This only stops renewal. The caller's current access token (JWT) is NOT revoked by this
    /// call — JWTs are stateless and not revocable server-side, so it stays valid until it
    /// expires naturally (see JwtSettings.AccessTokenExpirationMinutes).
    /// </remarks>
    Task LogoutAsync(Guid userId, LogoutRequest request, CancellationToken ct);
}
