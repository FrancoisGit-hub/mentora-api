using FluentValidation;
using Mentora.Core.DTOs.Stripe;

namespace Mentora.Core.Validators.Stripe;

public class SimulateWebhookRequestValidator : AbstractValidator<SimulateWebhookRequest>
{
    private static readonly string[] AllowedEventTypes =
    [
        "checkout.session.completed",
        "checkout.session.expired",
        "payment_intent.payment_failed",
    ];

    public SimulateWebhookRequestValidator()
    {
        RuleFor(x => x.EventType)
            .NotEmpty()
            .Must(t => AllowedEventTypes.Contains(t))
            .WithMessage($"EventType must be one of: {string.Join(", ", AllowedEventTypes)}.");

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .Must(s => s.StartsWith("cs_"))
            .WithMessage("SessionId must start with 'cs_'.");
    }
}
