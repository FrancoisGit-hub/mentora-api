using Mentora.Core.DTOs.Auth;

namespace Mentora.Core.Interfaces;

public interface IAuthService
{
    Task RequestOtpAsync(string email);
    Task<AuthResponse> VerifyOtpAsync(string email, string code);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
}
