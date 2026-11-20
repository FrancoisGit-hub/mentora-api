using Mentora.Core.Options;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services.Email;

/// <summary>Lot 14.1 (14.A9). Failure messages never contain the install URL.</summary>
public sealed class MemberInviteOptionsValidator : IValidateOptions<MemberInviteOptions>
{
    public ValidateOptionsResult Validate(string? name, MemberInviteOptions options)
    {
        var error = options.Validate();
        return error == null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(error);
    }
}
