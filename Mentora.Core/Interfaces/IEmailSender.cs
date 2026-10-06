namespace Mentora.Core.Interfaces;

public interface IEmailSender
{
    /// <summary>
    /// Sends an email. Implementations are responsible for retry and error handling.
    /// Must NOT throw — failures are logged internally and swallowed so callers never break on email errors.
    /// </summary>
    /// <param name="message">Envelope: To and Bcc addresses, subject, HTML body and plain-text fallback.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>true if successfully sent, false otherwise.</returns>
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct);
}
