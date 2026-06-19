namespace Mentora.Core.Interfaces;

public interface IEmailSender
{
    /// <summary>
    /// Sends an email. Implementations are responsible for retry and error handling.
    /// Must NOT throw — failures are logged internally and swallowed so callers never break on email errors.
    /// </summary>
    /// <param name="toAddress">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML body (multipart will be built with a plain-text fallback).</param>
    /// <param name="textBody">Plain-text fallback body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>true if successfully sent, false otherwise.</returns>
    Task<bool> SendAsync(string toAddress, string subject, string htmlBody, string textBody, CancellationToken ct);
}
