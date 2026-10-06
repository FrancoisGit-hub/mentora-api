namespace Mentora.Core.Interfaces;

/// <summary>
/// Envelope handed to <see cref="IEmailSender"/>. <paramref name="To"/> holds the real recipients;
/// the routing decorator may replace them (TEST mode) and add blind copies (PRODUCTION mode).
/// </summary>
public sealed record EmailMessage(
    IReadOnlyList<string> To,
    IReadOnlyList<string> Bcc,
    string Subject,
    string HtmlBody,
    string TextBody)
{
    public static EmailMessage ForRecipient(string toAddress, string subject, string htmlBody, string textBody)
        => new([toAddress], [], subject, htmlBody, textBody);
}
