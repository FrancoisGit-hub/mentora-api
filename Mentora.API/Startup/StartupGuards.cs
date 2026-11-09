using Mentora.Core.Options;
using Microsoft.Extensions.Options;

namespace Mentora.API.Startup;

/// <summary>
/// Lot 12.5a. Outside Development the public defaults of appsettings.json (app JWT secret,
/// connection string) must never be used: the values are read from the environment variables
/// themselves, not through configuration. Messages never contain a value.
/// </summary>
public static class StartupGuards
{
    public const int MinJwtSecretLength = 32;

    public static void VerifyOutsideDevelopment(IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            return;

        var secret = Environment.GetEnvironmentVariable("Jwt__Secret");
        if (string.IsNullOrEmpty(secret))
            throw new InvalidOperationException(
                $"Jwt__Secret must be set as an environment variable outside Development (the appsettings.json default is never accepted).");
        if (secret.Length < MinJwtSecretLength)
            throw new InvalidOperationException(
                $"Jwt__Secret must be at least {MinJwtSecretLength} characters.");

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
            throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection must be set as an environment variable outside Development (the appsettings.json default is never accepted).");
    }
}

/// <summary>Lot 12.5a. Failure messages never contain an origin.</summary>
public sealed class CorsOptionsValidator(IHostEnvironment environment) : IValidateOptions<CorsOptions>
{
    public ValidateOptionsResult Validate(string? name, CorsOptions options)
    {
        var error = options.Validate(environment.IsDevelopment());
        return error == null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(error);
    }
}
