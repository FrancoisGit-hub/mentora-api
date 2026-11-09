using Mentora.Core.Options;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services.Email;

/// <summary>Lot 11.4 (11.AB). Failure messages come from the options and never contain a value.</summary>
public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var error = options.Validate();
        return error == null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(error);
    }
}

/// <summary>Lot 11.4 (11.Z). Failure messages never contain an address.</summary>
public sealed class EmailRoutingOptionsValidator : IValidateOptions<EmailRoutingOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailRoutingOptions options)
    {
        var error = options.Validate();
        return error == null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(error);
    }
}

/// <summary>Lot 12.1. Failure messages never contain an address or a secret.</summary>
public sealed class AdminAuthOptionsValidator : IValidateOptions<AdminAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AdminAuthOptions options)
    {
        var error = options.Validate();
        return error == null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(error);
    }
}
