using System.Net.Mail;

namespace Mentora.Core.Options;

/// <summary>
/// Lot 12.1 — admin back-office authentication. JwtSecret and Emails come from environment
/// variables only (AdminAuth__JwtSecret, AdminAuth__Emails); appsettings.json keeps them empty.
/// </summary>
public sealed class AdminAuthOptions
{
    public const string SectionName = "AdminAuth";

    public string JwtSecret { get; init; } = "";
    public string JwtIssuer { get; init; } = "mentora-admin";
    public string JwtAudience { get; init; } = "mentora-admin";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenHours { get; init; } = 12;
    public int OtpLifetimeMinutes { get; init; } = 10;
    public int OtpMaxAttempts { get; init; } = 5;
    public int RequestPermitLimit { get; init; } = 30;
    public int RequestWindowMinutes { get; init; } = 15;

    /// <summary>Comma separated list of the admin addresses.</summary>
    public string Emails { get; init; } = "";

    /// <summary>Admin addresses, trimmed and lower case (the form stored in ADMIN_USERS).</summary>
    public IReadOnlyList<string> EmailList => Split(Emails).Select(e => e.ToLowerInvariant()).ToList();

    /// <summary>Returns a failure message (never containing a value) or null when valid.</summary>
    public string? Validate()
    {
        var emails = Split(Emails);
        if (emails.Count == 0)
            return "AdminAuth:Emails must contain at least one address.";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < emails.Count; i++)
        {
            if (emails[i].Length > 255
                || !MailAddress.TryCreate(emails[i], out var parsed)
                || !string.Equals(parsed.Address, emails[i], StringComparison.OrdinalIgnoreCase))
                return $"AdminAuth:Emails item #{i + 1} is not a valid email address.";
            if (!seen.Add(emails[i]))
                return $"AdminAuth:Emails item #{i + 1} is a duplicate (case insensitive).";
        }

        if (string.IsNullOrEmpty(JwtSecret) || JwtSecret.Length < 32)
            return "AdminAuth:JwtSecret must be at least 32 characters.";
        if (string.IsNullOrWhiteSpace(JwtIssuer))
            return "AdminAuth:JwtIssuer must be set.";
        if (string.IsNullOrWhiteSpace(JwtAudience))
            return "AdminAuth:JwtAudience must be set.";
        if (AccessTokenMinutes is < 1 or > 60)
            return "AdminAuth:AccessTokenMinutes must be between 1 and 60.";
        if (RefreshTokenHours is < 1 or > 168)
            return "AdminAuth:RefreshTokenHours must be between 1 and 168.";
        if (OtpLifetimeMinutes is < 1 or > 30)
            return "AdminAuth:OtpLifetimeMinutes must be between 1 and 30.";
        if (OtpMaxAttempts is < 1 or > 10)
            return "AdminAuth:OtpMaxAttempts must be between 1 and 10.";
        if (RequestPermitLimit <= 0)
            return "AdminAuth:RequestPermitLimit must be greater than 0.";
        if (RequestWindowMinutes <= 0)
            return "AdminAuth:RequestWindowMinutes must be greater than 0.";
        return null;
    }

    private static IReadOnlyList<string> Split(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
