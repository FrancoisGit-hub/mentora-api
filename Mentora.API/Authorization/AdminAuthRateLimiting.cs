using System.Threading.RateLimiting;
using Mentora.Core.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Mentora.API.Authorization;

public static class AdminAuthRateLimiting
{
    /// <summary>
    /// Lot 12.1 — fixed window per client IP on every /api/v1/admin/auth route, whatever the
    /// Authorization header (the IP is the real one only because UseForwardedHeaders runs first).
    /// Rejection status and OnRejected (429 envelope + Retry-After) are the shared ones set by
    /// AddQuizPolicy: not reassigned here.
    /// </summary>
    public static void AddAdminAuthPolicy(this RateLimiterOptions limiter)
    {
        limiter.AddPolicy(AdminAuthDefaults.RateLimitPolicy, httpContext =>
        {
            var admin = httpContext.RequestServices.GetRequiredService<IOptions<AdminAuthOptions>>().Value;
            var ip    = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit       = admin.RequestPermitLimit,
                Window            = TimeSpan.FromMinutes(admin.RequestWindowMinutes),
                QueueLimit        = 0,
                AutoReplenishment = true,
            });
        });
    }
}
