namespace Mentora.Core.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; init; } = "Logging"; // "Smtp" or "Logging"
    public string FromAddress { get; init; } = "";
    public string FromDisplayName { get; init; } = "Mentora";
    public SmtpSettings Smtp { get; init; } = new();
    public RetrySettings Retry { get; init; } = new();
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
