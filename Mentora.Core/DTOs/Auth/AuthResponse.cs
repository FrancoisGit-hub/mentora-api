namespace Mentora.Core.DTOs.Auth;

public record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn);
