using Mentora.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Services.Email;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogInformation(
            "[EMAIL-LOG] To={To} Subject={Subject} TextBody={TextBody}",
            string.Join(",", message.To), message.Subject, message.TextBody);
        return Task.FromResult(true);
    }
}
