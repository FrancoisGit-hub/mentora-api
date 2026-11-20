using System.Threading.RateLimiting;
using Mentora.Core.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Mentora.API.Authorization;

/// <summary>Lot 14.2 (14.C6): IP rate limit of POST /api/v1/auth/otp/request and /api/v1/auth/otp/verify.</summary>
public static class AuthOtpRateLimiting
{
    public const string PolicyName = "AppAuthOtp";

    /// <summary>
    /// Fixed window per client IP (the real one only because UseForwardedHeaders runs first), limits from the
    /// AuthRateLimit options, no queue. Both OTP routes use the same policy, so they share one window per IP.
    /// The key is never null ("no-ip" when the address is unknown). Not applied to token refresh nor logout.
    /// Rejection status and OnRejected (429 envelope and Retry-After) are the shared ones set by
    /// AddQuizPolicy: not reassigned here.
    /// </summary>
    public static void AddAuthOtpPolicy(this RateLimiterOptions limiter)
    {
        limiter.AddPolicy(PolicyName, httpContext =>
        {
            var ip   = httpContext.Connection.RemoteIpAddress?.ToString() ?? "no-ip";
            var auth = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;

            return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit       = auth.PermitLimit,
                Window            = TimeSpan.FromMinutes(auth.WindowMinutes),
                QueueLimit        = 0,
                AutoReplenishment = true,
            });
        });
    }
}
