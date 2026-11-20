namespace Mentora.Core.Options;

/// <summary>
/// Lot 14.1 (14.A7, 14.A9): settings of the "coach adds a member" feature, section "MemberInvite"
/// (env vars MemberInvite__InstallUrl, MemberInvite__PermitLimit, MemberInvite__WindowMinutes).
/// </summary>
public sealed class MemberInviteOptions
{
    public const string SectionName = "MemberInvite";

    /// <summary>Optional link to install the app. When set it must be an absolute https URL.</summary>
    public string? InstallUrl { get; init; }

    /// <summary>Members a coach may add per window (1 to 1000).</summary>
    public int PermitLimit { get; init; } = 30;

    /// <summary>Window length in minutes (1 to 1440).</summary>
    public int WindowMinutes { get; init; } = 60;

    public bool HasInstallUrl => !string.IsNullOrWhiteSpace(InstallUrl);

    /// <summary>Returns a failure message (never containing the URL) or null when valid.</summary>
    public string? Validate()
    {
        if (PermitLimit is < 1 or > 1000)
            return "MemberInvite:PermitLimit must be between 1 and 1000.";
        if (WindowMinutes is < 1 or > 1440)
            return "MemberInvite:WindowMinutes must be between 1 and 1440.";
        if (HasInstallUrl)
        {
            var value = InstallUrl!.Trim();
            if (value.Any(char.IsWhiteSpace)
                || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrEmpty(uri.Host))
                return "MemberInvite:InstallUrl must be an absolute https URL.";
        }
        return null;
    }
}
