using Mentora.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Services.Email;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task<bool> SendAsync(string toAddress, string subject, string htmlBody, string textBody, CancellationToken ct)
    {
        logger.LogInformation(
            "[EMAIL-LOG] To={To} Subject={Subject} TextBody={TextBody}",
            toAddress, subject, textBody);
        return Task.FromResult(true);
    }
}
