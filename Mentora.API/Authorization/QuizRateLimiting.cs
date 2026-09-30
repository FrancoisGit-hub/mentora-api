using System.Threading.RateLimiting;
using Mentora.Core.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Mentora.API.Authorization;

public static class QuizRateLimiting
{
    public const string PolicyName = "QuizBasic";
    public const string DeviceIdHeader = "X-Device-Id";

    /// <summary>
    /// Requests without an Authorization header: fixed window per client IP (the IP is the real one
    /// only because UseForwardedHeaders runs first). Requests carrying an Authorization header are
    /// not limited here: a bad token is rejected by the authorization step before reaching the limiter.
    /// </summary>
    public static void AddQuizPolicy(this RateLimiterOptions limiter)
    {
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        limiter.AddPolicy(PolicyName, httpContext =>
        {
            if (httpContext.Request.Headers.ContainsKey(HeaderNames.Authorization))
                return RateLimitPartition.GetNoLimiter("authenticated");

            var quiz = httpContext.RequestServices.GetRequiredService<IOptions<QuizOptions>>().Value;
            var ip   = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = quiz.AnonymousPermitLimit,
                Window               = TimeSpan.FromMinutes(quiz.AnonymousWindowMinutes),
                QueueLimit           = 0,
                AutoReplenishment    = true,
            });
        });

        limiter.OnRejected = async (context, ct) =>
        {
            var response = context.HttpContext.Response;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

            response.StatusCode = StatusCodes.Status429TooManyRequests;
            await response.WriteAsJsonAsync(new
            {
                success    = false,
                data       = (object?)null,
                error      = "Too many requests.",
                statusCode = 429
            }, ct);
        };
    }
}
