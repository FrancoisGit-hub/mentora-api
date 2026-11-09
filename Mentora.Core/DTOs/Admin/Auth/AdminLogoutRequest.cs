namespace Mentora.Core.DTOs.Admin.Auth;

/// <summary>Lot 12.1 — revokes an admin refresh token. Always answered with the same 200.</summary>
public record AdminLogoutRequest(string RefreshToken);
