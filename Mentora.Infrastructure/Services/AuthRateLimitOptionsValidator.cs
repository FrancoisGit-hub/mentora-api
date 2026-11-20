using Mentora.Core.Options;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services;

/// <summary>Lot 14.2 (14.C6): startup validation of the "AuthRateLimit" section.</summary>
public sealed class AuthRateLimitOptionsValidator : IValidateOptions<AuthRateLimitOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthRateLimitOptions options)
    {
        var error = options.Validate();
        return error == null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(error);
    }
}
