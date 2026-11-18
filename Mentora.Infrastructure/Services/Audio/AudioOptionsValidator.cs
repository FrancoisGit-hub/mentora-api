using Mentora.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services.Audio;

/// <summary>
/// Lot 13.3. Rules in <see cref="AudioOptions.Validate"/>: MaxConcurrentDecodes 1 to 4 and
/// DecodeTimeoutSeconds 1 to 60 in every environment; outside Development StoragePath is required and
/// must be an absolute path. Failure messages name the setting, never a value.
/// </summary>
public sealed class AudioOptionsValidator(IHostEnvironment environment) : IValidateOptions<AudioOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AudioOptions options)
    {
        var error = options.Validate(environment.IsDevelopment());
        return error == null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(error);
    }
}
