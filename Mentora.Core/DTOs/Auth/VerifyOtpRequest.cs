namespace Mentora.Core.DTOs.Auth;

public record VerifyOtpRequest(string Email, string Code);
