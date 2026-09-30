using Microsoft.AspNetCore.Authorization;
using Microsoft.Net.Http.Headers;

namespace Mentora.API.Authorization;

/// <summary>
/// Quiz player: an anonymous device (no Authorization header at all) or a coach. An Authorization
/// header carrying an invalid/expired token never falls back to device mode (the request fails and
/// is challenged: 401); an authenticated non-coach is forbidden (403).
/// </summary>
public sealed class QuizPlayerRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "QuizPlayer";
}

public sealed class QuizPlayerHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<QuizPlayerRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, QuizPlayerRequirement requirement)
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null)
            return;

        if (!http.Request.Headers.ContainsKey(HeaderNames.Authorization))
        {
            context.Succeed(requirement);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
            return;

        // Same decision as the CoachOnly policy (COACH or BOTH), evaluated through it rather than
        // copied. IAuthorizationService is resolved per request: injecting it in the constructor
        // would be a cycle (the service depends on the handlers).
        var authorization = http.RequestServices.GetRequiredService<IAuthorizationService>();
        var coachOnly = await authorization.AuthorizeAsync(context.User, "CoachOnly");
        if (coachOnly.Succeeded)
            context.Succeed(requirement);
    }
}
