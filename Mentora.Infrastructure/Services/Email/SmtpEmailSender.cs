using MailKit.Net.Smtp;
using MailKit.Security;
using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Mentora.Infrastructure.Services.Email;

public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _opts = options.Value;

    public async Task<bool> SendAsync(string toAddress, string subject, string htmlBody, string textBody, CancellationToken ct)
    {
        var message = BuildMessage(toAddress, subject, htmlBody, textBody);

        var maxAttempts = Math.Max(1, _opts.Retry.MaxAttempts);
        var delay = TimeSpan.FromSeconds(Math.Max(0, _opts.Retry.DelaySeconds));

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var client = new SmtpClient();
                var secureSocketOption = _opts.Smtp.UseStartTls
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.SslOnConnect;

                await client.ConnectAsync(_opts.Smtp.Host, _opts.Smtp.Port, secureSocketOption, ct);
                await client.AuthenticateAsync(_opts.Smtp.Username, _opts.Smtp.Password, ct);
                await client.SendAsync(message, ct);
                await client.DisconnectAsync(quit: true, ct);

                logger.LogInformation(
                    "Email sent successfully. To={To} Subject={Subject} Attempt={Attempt}",
                    toAddress, subject, attempt);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "SMTP send attempt {Attempt}/{MaxAttempts} failed. To={To} Subject={Subject}",
                    attempt, maxAttempts, toAddress, subject);

                if (attempt < maxAttempts)
                {
                    try { await Task.Delay(delay, ct); }
                    catch (OperationCanceledException) { throw; }
                }
            }
        }

        logger.LogError(
            "SMTP send failed permanently after {MaxAttempts} attempts. To={To} Subject={Subject}",
            maxAttempts, toAddress, subject);
        return false;
    }

    private MimeMessage BuildMessage(string toAddress, string subject, string htmlBody, string textBody)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_opts.FromDisplayName, _opts.FromAddress));
        msg.To.Add(MailboxAddress.Parse(toAddress));
        msg.Subject = subject;
        msg.ReplyTo.Add(new MailboxAddress(_opts.FromDisplayName, _opts.FromAddress));

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody
        };
        msg.Body = builder.ToMessageBody();
        return msg;
    }
}
