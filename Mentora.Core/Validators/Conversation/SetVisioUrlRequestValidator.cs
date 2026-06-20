using FluentValidation;
using Mentora.Core.DTOs.Conversation;

namespace Mentora.Core.Validators.Conversation;

public sealed class SetVisioUrlRequestValidator : AbstractValidator<SetVisioUrlRequestDto>
{
    public SetVisioUrlRequestValidator()
    {
        RuleFor(x => x.Url)
            .Must(BeAcceptable)
            .WithMessage("Url must be a valid https:// URL with at most 2000 characters, or null to reset to the auto-generated Jitsi link.");
    }

    private static bool BeAcceptable(string? url)
    {
        if (url is null) return true;

        var trimmed = url.Trim();
        if (trimmed.Length == 0 || trimmed.Length > 2000) return false;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return false;

        return uri.Scheme == Uri.UriSchemeHttps;
    }
}
