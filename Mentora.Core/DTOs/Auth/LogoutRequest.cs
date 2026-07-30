namespace Mentora.Core.DTOs.Auth;

public record LogoutRequest(string RefreshToken, string? DeviceToken);
