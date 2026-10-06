using System.Net.Mail;

namespace Mentora.Core.Options;

/// <summary>
/// Lot 11.4 (11.Z) — routing applied to every outgoing mail. Anything but "Production" is TEST mode:
/// mails go only to <see cref="TestRecipients"/>. Addresses come from environment variables only
/// (EmailRouting__Mode, EmailRouting__TestRecipients, EmailRouting__ProductionBcc).
/// </summary>
public sealed class EmailRoutingOptions
{
    public const string SectionName = "EmailRouting";
    public const string ProductionMode = "Production";

    public string Mode { get; init; } = "";
    public string TestRecipients { get; init; } = "";
    public string ProductionBcc { get; init; } = "";

    public bool IsProduction => string.Equals(Mode?.Trim(), ProductionMode, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when Mode is set to something other than "Production" (probably a typo): TEST applies.</summary>
    public bool IsUnrecognizedMode => !IsProduction && !string.IsNullOrWhiteSpace(Mode);

    public string ModeLabel => IsProduction ? "PRODUCTION" : "TEST";

    public IReadOnlyList<string> TestRecipientList => Split(TestRecipients);

    public IReadOnlyList<string> ProductionBccList => Split(ProductionBcc);

    /// <summary>Returns a failure message (never containing an address) or null when valid.</summary>
    public string? Validate()
    {
        var test = TestRecipientList;
        if (test.Count == 0)
            return "EmailRouting:TestRecipients must contain at least one address.";
        var testError = ValidateAddresses(test, "EmailRouting:TestRecipients");
        if (testError != null)
            return testError;

        var bcc = ProductionBccList;
        if (IsProduction && bcc.Count == 0)
            return "EmailRouting:ProductionBcc must contain at least one address when EmailRouting:Mode is Production.";
        return ValidateAddresses(bcc, "EmailRouting:ProductionBcc");
    }

    private static string? ValidateAddresses(IReadOnlyList<string> addresses, string name)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < addresses.Count; i++)
        {
            if (!MailAddress.TryCreate(addresses[i], out var parsed)
                || !string.Equals(parsed.Address, addresses[i], StringComparison.OrdinalIgnoreCase))
                return $"{name} item #{i + 1} is not a valid email address.";
            if (!seen.Add(addresses[i]))
                return $"{name} item #{i + 1} is a duplicate (case insensitive).";
        }
        return null;
    }

    private static IReadOnlyList<string> Split(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
