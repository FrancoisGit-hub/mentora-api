using System.Security.Claims;
using System.Threading.RateLimiting;
using Mentora.Core.Messaging;
using Microsoft.AspNetCore.RateLimiting;

namespace Mentora.API.Authorization;

/// <summary>
/// Lot 13.4 (13.K): rate limit of the two voice upload routes.
/// </summary>
public static class VoiceUploadRateLimiting
{
    public const string PolicyName = "VoiceUpload";

    /// <summary>
    /// Fixed window per authenticated user: <see cref="VoiceMessageRules.VoiceUploadPermitLimit"/> requests per
    /// <see cref="VoiceMessageRules.VoiceUploadWindowMinutes"/> minutes, no queue. The partition key is the user id,
    /// read exactly like AccountController, AuthController and UserDevicesController: the app Bearer scheme maps
    /// inbound claims (MapInboundClaims = true), so the JWT "sub" claim arrives as
    /// <see cref="ClaimTypes.NameIdentifier"/>. The key is never null: without that claim (which cannot happen on
    /// these authenticated routes) the fixed key "no-user" is used. Every request that reaches the limiter counts,
    /// refused uploads included (the limiter runs after authorization, so a request without a valid token is
    /// answered 401 before it). Rejection status and OnRejected (429 envelope and Retry-After) are the shared ones
    /// set by AddQuizPolicy: not reassigned here.
    /// </summary>
    public static void AddVoiceUploadPolicy(this RateLimiterOptions limiter)
    {
        limiter.AddPolicy(PolicyName, httpContext =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "no-user";

            return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit       = VoiceMessageRules.VoiceUploadPermitLimit,
                Window            = TimeSpan.FromMinutes(VoiceMessageRules.VoiceUploadWindowMinutes),
                QueueLimit        = 0,
                AutoReplenishment = true,
            });
        });
    }
}
