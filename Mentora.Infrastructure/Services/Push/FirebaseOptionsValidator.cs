using Mentora.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services.Push;

/// <summary>
/// Lot 14.9b. Path set: the file must exist, parse, and be a service account of Firebase:ProjectId with an
/// importable key, in every environment. Path empty: allowed in Development (push not configured), refused
/// elsewhere. Messages never contain the path or any content of the file.
/// </summary>
public sealed class FirebaseOptionsValidator(IHostEnvironment environment) : IValidateOptions<FirebaseOptions>
{
    public ValidateOptionsResult Validate(string? name, FirebaseOptions options)
    {
        if (!options.IsConfigured)
        {
            return environment.IsDevelopment()
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(
                    "Firebase:ServiceAccountPath must be set as an environment variable outside Development.");
        }

        if (string.IsNullOrWhiteSpace(options.ProjectId))
            return ValidateOptionsResult.Fail("Firebase:ProjectId must be set.");

        var error = FirebaseServiceAccount.TryLoad(options.ServiceAccountPath, options.ProjectId, out var account);
        account?.Dispose();
        return error is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(error);
    }
}
