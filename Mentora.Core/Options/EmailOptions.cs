using System.Net.Mail;

namespace Mentora.Core.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public const string SmtpProvider = "Smtp";
    public const string LoggingProvider = "Logging";

    public string Provider { get; init; } = LoggingProvider; // "Smtp" or "Logging"
    public string FromAddress { get; init; } = "";
    public string FromDisplayName { get; init; } = "Mentora";
    public SmtpSettings Smtp { get; init; } = new();
    public RetrySettings Retry { get; init; } = new();

    public bool IsSmtp => string.Equals(Provider?.Trim(), SmtpProvider, StringComparison.OrdinalIgnoreCase);

    public bool IsLogging => string.Equals(Provider?.Trim(), LoggingProvider, StringComparison.OrdinalIgnoreCase);

    /// <summary>Canonical provider name ("Smtp" or "Logging"); only meaningful once <see cref="Validate"/> passed.</summary>
    public string EffectiveProvider => IsSmtp ? SmtpProvider : LoggingProvider;

    /// <summary>
    /// Lot 11.4 (11.AB). Returns a failure message or null when valid. Messages name the setting
    /// keys only, never a value. Logging needs nothing (it does not use FromAddress or Smtp).
    /// </summary>
    public string? Validate()
    {
        if (!IsSmtp && !IsLogging)
            return "Email:Provider must be Smtp or Logging.";
        if (IsLogging)
            return null;

        if (string.IsNullOrWhiteSpace(Smtp.Host))
            return "Email:Smtp:Host must be set when Email:Provider is Smtp.";
        if (Smtp.Port is < 1 or > 65535)
            return "Email:Smtp:Port must be between 1 and 65535.";
        if (string.IsNullOrWhiteSpace(FromAddress) || !MailAddress.TryCreate(FromAddress.Trim(), out _))
            return "Email:FromAddress must be a valid email address when Email:Provider is Smtp.";
        if (string.IsNullOrWhiteSpace(Smtp.Username))
            return "Email:Smtp:Username must be set when Email:Provider is Smtp.";
        if (string.IsNullOrWhiteSpace(Smtp.Password))
            return "Email:Smtp:Password must be set when Email:Provider is Smtp.";
        return null;
    }
}

public sealed class SmtpSettings
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;
    public bool UseStartTls { get; init; } = true;
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
}

public sealed class RetrySettings
{
    public int MaxAttempts { get; init; } = 2;
    public int DelaySeconds { get; init; } = 2;
}
