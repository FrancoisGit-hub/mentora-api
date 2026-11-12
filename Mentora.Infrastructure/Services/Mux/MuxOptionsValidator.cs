using Mentora.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services.Mux;

/// <summary>
/// Lot 12.3a. Rules in <see cref="MuxOptions.Validate"/>: outside Development TokenId, TokenSecret and
/// WebhookSecret must be non-empty and WebhookSecret must contain no whitespace (no minimum length: MUX
/// generates it). Failure messages never contain a token, a secret or an origin.
/// </summary>
public sealed class MuxOptionsValidator(IHostEnvironment environment) : IValidateOptions<MuxOptions>
{
    public ValidateOptionsResult Validate(string? name, MuxOptions options)
    {
        var error = options.Validate(environment.IsDevelopment());
        return error == null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(error);
    }
}
