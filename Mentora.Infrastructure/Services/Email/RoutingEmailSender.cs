using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Services.Email;

/// <summary>
/// Lot 11.4 (11.Z) — the single layer every mail goes through. TEST mode (default): the mail goes only
/// to the configured test recipients, never to the real one, with a "[TEST pour ...]" subject prefix.
/// PRODUCTION: real recipient, plus the configured blind copies. Never logs the body (it holds the OTP).
/// </summary>
public sealed class RoutingEmailSender(
    IEmailSender inner,
    EmailRoutingOptions routing,
    ILogger<RoutingEmailSender> logger) : IEmailSender
{
    public Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var routed = Route(message);
        logger.LogInformation(
            "[EMAIL] mode={Mode} to={To} bcc={Bcc} subject={Subject}",
            routing.ModeLabel, string.Join(",", routed.To), string.Join(",", routed.Bcc), routed.Subject);
        return inner.SendAsync(routed, ct);
    }

    private EmailMessage Route(EmailMessage message)
    {
        if (!routing.IsProduction)
        {
            var original = string.Join(", ", message.To);
            return message with
            {
                To = routing.TestRecipientList,
                Bcc = [],
                Subject = $"[TEST pour {original}] {message.Subject}"
            };
        }

        var bcc = new List<string>();
        foreach (var address in message.Bcc.Concat(routing.ProductionBccList))
        {
            var alreadyPresent = message.To.Concat(bcc)
                .Any(a => string.Equals(a, address, StringComparison.OrdinalIgnoreCase));
            if (!alreadyPresent)
                bcc.Add(address);
        }
        return message with { Bcc = bcc };
    }
}
