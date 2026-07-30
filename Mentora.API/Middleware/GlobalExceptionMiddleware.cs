using FluentValidation;
using Mentora.Core.Exceptions;

namespace Mentora.API.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "Not found: {Message}", ex.Message);
            await WriteResponseAsync(context, 404, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            logger.LogWarning(ex, "Forbidden: {Message}", ex.Message);
            await WriteResponseAsync(context, 403, ex.Message);
        }
        catch (ConflictException ex)
        {
            logger.LogWarning(ex, "Conflict: {Message}", ex.Message);
            await WriteResponseAsync(context, 409, ex.Message, ex.Details);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Validation failed");
            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());
            context.Response.StatusCode  = 422;
            context.Response.ContentType = "application/json";
            // Same envelope as every other error response ({success, data, error, statusCode}),
            // plus the per-field detail. Field-name casing in `errors` is whatever
            // FluentValidation's PropertyName is (PascalCase — the C# property name, since no
            // validator in this project calls .WithName(...) to override it).
            await context.Response.WriteAsJsonAsync(new
            {
                success    = false,
                data       = (object?)null,
                error      = "Validation failed.",
                statusCode = 422,
                errors
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Bad request: {Message}", ex.Message);
            await WriteResponseAsync(context, 400, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteResponseAsync(context, 500, "An unexpected error occurred.");
        }
    }

    private static async Task WriteResponseAsync(HttpContext context, int statusCode, string error,
        object? details = null)
    {
        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/json";
        if (details is not null)
        {
            await context.Response.WriteAsJsonAsync(new
            {
                success    = false,
                data       = (object?)null,
                error,
                details,
                statusCode
            });
        }
        else
        {
            await context.Response.WriteAsJsonAsync(new
            {
                success    = false,
                data       = (object?)null,
                error,
                statusCode
            });
        }
    }
}
