using Mentora.Core.DTOs.Auth;

namespace Mentora.Core.Interfaces;

public interface IAuthService
{
    Task RequestOtpAsync(string email, bool? isCoach);
    Task<AuthResponse> VerifyOtpAsync(string email, string code, bool? isCoach);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
}
