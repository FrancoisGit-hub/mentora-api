using System.Net;
using Mentora.Core;

namespace Mentora.Infrastructure.Services.Email;

/// <summary>
/// Lot 14.1 (14.A8): subject, HTML and text bodies of the mail sent to a member a coach just added.
/// Names are HTML encoded in the HTML body and stripped of control characters everywhere.
/// The button is a colored border on a white background (Gmail mobile removes backgrounds).
/// Review wave 1 (F1): the app asks no role any more; the member types the email and taps « Connexion ».
/// </summary>
internal static class MemberAddedEmailBuilder
{
    private const string StepInstallWithButton = "Installez l'application Mentora avec le bouton ci-dessous.";
    private const string StepInstallNoLink = "Votre coach vous enverra le lien pour installer l'application Mentora.";
    private const string StepCode = "Entrez le code à 6 chiffres que vous recevez par email.";
    private const string NoSignUpSentence = "Ne passez pas par « Créez un compte » : votre compte est déjà prêt.";

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
        var email = WebUtility.HtmlEncode(EmailNormalizer.Normalize(memberEmail));
        var stepInstall = WebUtility.HtmlEncode(installUrl == null ? StepInstallNoLink : StepInstallWithButton);
        var installBlock = installUrl == null
            ? ""
            : $@"
  <p><a href=""{WebUtility.HtmlEncode(installUrl)}"" style=""display: inline-block; color: #1a1a1a; background-color: #ffffff; border: 2px solid #1a1a1a; border-radius: 8px; padding: 12px 24px; font-weight: bold; text-decoration: none;"">Installer l'application</a></p>";

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
    <li>{stepInstall}</li>
    <li>Ouvrez-la, saisissez votre adresse email (<strong>{email}</strong>) et appuyez sur « Connexion ».</li>
    <li>{WebUtility.HtmlEncode(StepCode)}</li>
  </ol>{installBlock}
  <p>{WebUtility.HtmlEncode(NoSignUpSentence)}</p>
  <p style=""color: #666; font-size: 13px; margin-top: 32px;"">
    Si vous ne connaissez pas ce coach, ignorez ce message.
  </p>
</body>
</html>";
    }

    public static string BuildText(string coachFullName, string memberEmail, string? installUrl)
    {
        var email = EmailNormalizer.Normalize(memberEmail);
        var stepInstall = installUrl == null ? StepInstallNoLink : StepInstallWithButton;
        var installLine = installUrl == null
            ? ""
            : $@"Installer l'application : {installUrl}

";

        return $@"Mentora

Bonjour,

{coachFullName} vous a ajouté sur Mentora.

Pour commencer :
1. {stepInstall}
2. Ouvrez-la, saisissez votre adresse email ({email}) et appuyez sur « Connexion ».
3. {StepCode}

{installLine}{NoSignUpSentence}

Si vous ne connaissez pas ce coach, ignorez ce message.";
    }
}
