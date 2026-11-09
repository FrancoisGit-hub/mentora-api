namespace Mentora.Core.DTOs.Admin.Auth;

/// <summary>
/// Lot 12.1 — admin token pair. ExpiresIn and RefreshExpiresIn are in seconds.
/// The refresh token is opaque ("{id}:{secret}") and single use (rotated at each refresh).
/// </summary>
public record AdminAuthResponse(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    int RefreshExpiresIn,
    AdminMeResponse Admin);
