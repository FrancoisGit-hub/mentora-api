namespace Mentora.Core.DTOs.Admin.Auth;

/// <summary>Lot 12.1 — rotates an admin refresh token ("{id}:{secret}", single use).</summary>
public record AdminRefreshRequest(string RefreshToken);
