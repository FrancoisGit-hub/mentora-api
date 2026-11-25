using System.Globalization;
using System.Text.RegularExpressions;

namespace Mentora.Core.Notifications;

/// <summary>
/// Lot 14.9b. French texts of the notifications (vouvoiement for members). Dates are shown in Europe/Paris,
/// format "dd/MM à HH'h'mm" (26/11 à 18h30). When the time zone cannot be resolved the " le {date}" / " du {date}"
/// part is dropped rather than showing UTC.
/// </summary>
public static class NotificationTexts
{
    public const int TitleMaxLength = 100;
    public const int BodyMaxLength = 200;
    public const int ChatPreviewMaxLength = 120;
    public const string AudioBody = "Message vocal";

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Europe/Paris, or null when neither the IANA nor the Windows id is known on this machine.</summary>
    public static TimeZoneInfo? ParisTimeZone { get; } = ResolveParis();

    private static TimeZoneInfo? ResolveParis()
    {
        foreach (var id in new[] { "Europe/Paris", "Romance Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return null;
    }

    /// <summary>"26/11 à 18h30" or null when the time zone is unavailable.</summary>
    public static string? FormatDate(DateTime scheduledAtUtc)
    {
        if (ParisTimeZone is null)
            return null;
        var utc = DateTime.SpecifyKind(scheduledAtUtc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, ParisTimeZone);
        return local.ToString("dd/MM 'à' HH'h'mm", CultureInfo.InvariantCulture);
    }

    public static string FullName(string firstName, string lastName) => $"{firstName} {lastName}".Trim();

    /// <summary>Text trimmed, collapsed to one line, cut at 120 characters with "..." when cut. Null means audio.</summary>
    public static string ChatBody(string? textContent)
    {
        if (textContent is null)
            return AudioBody;

        var line = Whitespace.Replace(textContent, " ").Trim();
        if (line.Length <= ChatPreviewMaxLength)
            return line;

        var cut = ChatPreviewMaxLength;
        if (char.IsHighSurrogate(line[cut - 1]))
            cut--; // never split a surrogate pair
        return line[..cut] + "...";
    }

    public static (string Title, string Body) SessionBookedForCoach(string memberName, DateTime scheduledAtUtc) =>
        ("Nouvelle réservation", $"{memberName} a réservé une séance{Date(" le ", scheduledAtUtc)}.");

    public static (string Title, string Body) SessionBookedForMember(string coachFirstName, DateTime scheduledAtUtc) =>
        ("Nouvelle séance", $"{coachFirstName} vous a inscrit à une séance{Date(" le ", scheduledAtUtc)}.");

    public static (string Title, string Body) SessionCancelledForCoach(string memberName, DateTime scheduledAtUtc) =>
        ("Séance annulée", $"{memberName} a annulé la séance{Date(" du ", scheduledAtUtc)}.");

    public static (string Title, string Body) SessionCancelledForMember(string coachFirstName, DateTime scheduledAtUtc) =>
        ("Séance annulée", $"Votre séance{Date(" du ", scheduledAtUtc)} avec {coachFirstName} est annulée.");

    public static (string Title, string Body) MemberJoined(string memberName) =>
        ("Nouvel élève connecté", $"{memberName} vient de se connecter à Mentora.");

    /// <summary>Keeps a text inside a column limit (names are free text). Never splits a surrogate pair.</summary>
    public static string Fit(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        var cut = maxLength;
        if (char.IsHighSurrogate(value[cut - 1]))
            cut--;
        return value[..cut];
    }

    private static string Date(string prefix, DateTime scheduledAtUtc)
    {
        var date = FormatDate(scheduledAtUtc);
        return date is null ? "" : prefix + date;
    }
}
