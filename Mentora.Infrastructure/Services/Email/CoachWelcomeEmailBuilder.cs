using System.Net;
using Mentora.Core;

namespace Mentora.Infrastructure.Services.Email;

/// <summary>
/// Lot 14.8a (14.G12): subject, HTML and text bodies of the mail sent to a coach the Mentora team just created.
/// Same structure and styles as MemberAddedEmailBuilder: values are HTML encoded in the HTML body, the button is
/// a colored border on a white background (Gmail mobile removes backgrounds) and only exists when the link is set.
/// The first name is expected already cleaned by AuthService.SanitizeFirstName (null when nothing is left).
/// </summary>
internal static class CoachWelcomeEmailBuilder
{
    private const string Intro = "L'équipe Mentora vient de créer votre compte coach. Pour vous connecter :";
    private const string StepInstall = "Installez l'application Mentora sur votre téléphone.";
    private const string StepCode = "Entrez le code à 6 chiffres reçu par mail.";
    private const string NoSignUpSentence = "Ne passez pas par « Créez un compte » : votre compte est déjà prêt.";
    private const string NoLinkSentence = "L'équipe Mentora vous enverra le lien pour installer l'application.";
    private const string Footer = "Si vous n'attendiez pas ce message, ignorez-le.";
    private const string ButtonLabel = "Installer l'application";

    public static string BuildSubject(string? firstName)
        => firstName == null
            ? "Votre compte coach Mentora est prêt"
            : $"{firstName}, votre compte coach Mentora est prêt";

    public static string BuildHtml(string? firstName, string coachEmail, string? installUrl)
    {
        var greeting = WebUtility.HtmlEncode(firstName == null ? "Bonjour," : $"Bonjour {firstName},");
        var email = WebUtility.HtmlEncode(EmailNormalizer.Normalize(coachEmail));
        var installBlock = installUrl == null
            ? $@"
  <p>{WebUtility.HtmlEncode(NoLinkSentence)}</p>"
            : $@"
  <p><a href=""{WebUtility.HtmlEncode(installUrl)}"" style=""display: inline-block; color: #1a1a1a; background-color: #ffffff; border: 2px solid #1a1a1a; border-radius: 8px; padding: 12px 24px; font-weight: bold; text-decoration: none;"">{WebUtility.HtmlEncode(ButtonLabel)}</a></p>";

        return $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; color: #1a1a1a; background-color: #ffffff; max-width: 560px; margin: 0 auto; padding: 24px;"">
  <h1 style=""color: #1a1a1a; font-size: 20px;"">Mentora</h1>
  <p>{greeting}</p>
  <p>{WebUtility.HtmlEncode(Intro)}</p>
  <ol>
    <li>{WebUtility.HtmlEncode(StepInstall)}</li>
    <li>Ouvrez-la, saisissez votre adresse <strong>{email}</strong> et appuyez sur « Connexion ».</li>
    <li>{WebUtility.HtmlEncode(StepCode)}</li>
  </ol>{installBlock}
  <p>{WebUtility.HtmlEncode(NoSignUpSentence)}</p>
  <p style=""color: #666; font-size: 13px; margin-top: 32px;"">
    {WebUtility.HtmlEncode(Footer)}
  </p>
</body>
</html>";
    }

    public static string BuildText(string? firstName, string coachEmail, string? installUrl)
    {
        var greeting = firstName == null ? "Bonjour," : $"Bonjour {firstName},";
        var email = EmailNormalizer.Normalize(coachEmail);
        var installLine = installUrl == null
            ? $@"{NoLinkSentence}

"
            : $@"Installer l'application : {installUrl}

";

        return $@"Mentora

{greeting}

{Intro}
1. {StepInstall}
2. Ouvrez-la, saisissez votre adresse {email} et appuyez sur « Connexion ».
3. {StepCode}

{installLine}{NoSignUpSentence}

{Footer}";
    }
}
