using System.Threading.RateLimiting;
using Mentora.Core.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Mentora.API.Authorization;

/// <summary>Lot 14.1 (14.A7): rate limit of POST /api/v1/coach/members.</summary>
public static class MemberInviteRateLimiting
{
    public const string PolicyName = "CoachAddMember";

    /// <summary>
    /// Fixed window per coach (the "coachId" claim), limits from the MemberInvite options, no queue.
    /// The key is never null: without the claim (which cannot happen behind CoachOnly) "no-coach" is used.
    /// Every request that reaches the limiter counts, refused ones included. Rejection status and OnRejected
    /// (429 envelope and Retry-After) are the shared ones set by AddQuizPolicy: not reassigned here.
    /// </summary>
    public static void AddMemberInvitePolicy(this RateLimiterOptions limiter)
    {
        limiter.AddPolicy(PolicyName, httpContext =>
        {
            var coachId = httpContext.User.FindFirst("coachId")?.Value ?? "no-coach";
            var invite  = httpContext.RequestServices.GetRequiredService<IOptions<MemberInviteOptions>>().Value;

            return RateLimitPartition.GetFixedWindowLimiter(coachId, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit       = invite.PermitLimit,
                Window            = TimeSpan.FromMinutes(invite.WindowMinutes),
                QueueLimit        = 0,
                AutoReplenishment = true,
            });
        });
    }
}
