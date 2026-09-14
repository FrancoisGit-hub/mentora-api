using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mentora.API.Middleware;

/// <summary>
/// With <c>SuppressModelStateInvalidFilter</c> enabled, ASP.NET Core no longer auto-400s a
/// missing/malformed [FromBody] payload — binding just leaves the parameter null and lets the
/// action run. Every action here assumes its request DTO is non-null before touching it, so this
/// filter turns a null [FromBody] argument into a clean 400 (enveloped, via InvalidOperationException)
/// before the action executes.
/// </summary>
public class NullBodyGuardFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return;

        foreach (var parameter in descriptor.Parameters)
        {
            if (parameter.BindingInfo?.BindingSource?.Id != "Body")
                continue;

            // A body that fails to bind (missing, empty, or malformed JSON) is left out of
            // ActionArguments entirely rather than present-with-null — both cases mean "no body".
            var hasValue = context.ActionArguments.TryGetValue(parameter.Name, out var value);
            if (!hasValue || value is null)
                throw new InvalidOperationException("Request body is required.");
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
