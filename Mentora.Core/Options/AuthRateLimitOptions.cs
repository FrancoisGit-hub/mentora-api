namespace Mentora.Core.Options;

/// <summary>
/// Lot 14.2 (14.C6): IP rate limit of POST /api/v1/auth/otp/request and /api/v1/auth/otp/verify, section
/// "AuthRateLimit" (env vars AuthRateLimit__PermitLimit, AuthRateLimit__WindowMinutes).
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "AuthRateLimit";

    /// <summary>Calls per IP and per window, both OTP routes together (1 to 1000).</summary>
    public int PermitLimit { get; init; } = 30;

    /// <summary>Window length in minutes (1 to 1440).</summary>
    public int WindowMinutes { get; init; } = 15;

    /// <summary>Returns a failure message or null when valid.</summary>
    public string? Validate()
    {
        if (PermitLimit is < 1 or > 1000)
            return "AuthRateLimit:PermitLimit must be between 1 and 1000.";
        if (WindowMinutes is < 1 or > 1440)
            return "AuthRateLimit:WindowMinutes must be between 1 and 1440.";
        return null;
    }
}
