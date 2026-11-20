using System.Net;

namespace Mentora.Infrastructure.Services.Email;

/// <summary>
/// Lot 14.1 (14.A8): subject, HTML and text bodies of the mail sent to a member a coach just added.
/// Names are HTML encoded in the HTML body and stripped of control characters everywhere.
/// The button is a colored border on a white background (Gmail mobile removes backgrounds).
/// </summary>
internal static class MemberAddedEmailBuilder
{
    private const string NoLinkSentence = "Votre coach vous enverra le lien pour installer l'application.";

    public static string BuildSubject(string? coachFirstName)
        => coachFirstName == null
            ? "Votre coach vous a ajouté sur Mentora"
            : $"Votre coach {coachFirstName} vous a ajouté sur Mentora";

    /// <summary>Trims and drops control characters; null when nothing is left.</summary>
    public static string? CleanName(string? value)
    {
        if (value == null)
            return null;
        var cleaned = new string(value.Trim().Where(c => !char.IsControl(c)).ToArray()).Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    public static string BuildHtml(string coachFullName, string memberEmail, string? installUrl)
    {
        var coach = WebUtility.HtmlEncode(coachFullName);
        var email = WebUtility.HtmlEncode(memberEmail);
        var installBlock = installUrl == null
            ? $"<p>{WebUtility.HtmlEncode(NoLinkSentence)}</p>"
            : $@"<p><a href=""{WebUtility.HtmlEncode(installUrl)}"" style=""display: inline-block; color: #1a1a1a; background-color: #ffffff; border: 2px solid #1a1a1a; border-radius: 8px; padding: 12px 24px; font-weight: bold; text-decoration: none;"">Installer l'application</a></p>";

        return $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; color: #1a1a1a; background-color: #ffffff; max-width: 560px; margin: 0 auto; padding: 24px;"">
  <h1 style=""color: #1a1a1a; font-size: 20px;"">Mentora</h1>
  <p>Bonjour,</p>
  <p>{coach} vous a ajouté sur Mentora.</p>
  <p>Pour commencer :</p>
  <ol>
    <li>Installez l'application Mentora.</li>
    <li>Ouvrez-la et choisissez « élève ».</li>
    <li>Saisissez cette adresse e-mail : <strong>{email}</strong></li>
    <li>Entrez le code que vous recevez par e-mail.</li>
  </ol>
  {installBlock}
  <p style=""color: #666; font-size: 13px; margin-top: 32px;"">
    Si vous ne connaissez pas ce coach, ignorez ce message.
  </p>
</body>
</html>";
    }

    public static string BuildText(string coachFullName, string memberEmail, string? installUrl)
    {
        var installLine = installUrl == null
            ? NoLinkSentence
            : $"Installer l'application : {installUrl}";

        return $@"Mentora

Bonjour,

{coachFullName} vous a ajouté sur Mentora.

Pour commencer :
1. Installez l'application Mentora.
2. Ouvrez-la et choisissez « élève ».
3. Saisissez cette adresse e-mail : {memberEmail}
4. Entrez le code que vous recevez par e-mail.

{installLine}

Si vous ne connaissez pas ce coach, ignorez ce message.";
    }
}
