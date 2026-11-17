namespace Mentora.Core.Options;

/// <summary>
/// Lot 12.5a. Allowed browser origins, from the environment variable Cors__AllowedOrigins (comma
/// separated). In Development the local origins are added by Program.cs.
/// Lot 13.1. Debug origins, from Cors__DebugOrigins (comma separated), feed the same policy in every
/// environment: only http(s)://localhost with an explicit non default port, at most 5 items.
/// Outside Development a localhost origin is refused in Cors__AllowedOrigins (use Cors__DebugOrigins).
/// Failure messages never contain a value.
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Maximum number of items accepted in <see cref="DebugOrigins"/>.</summary>
    public const int MaxDebugOrigins = 5;

    /// <summary>Comma separated list of origins, for example "https://admin.mentorapp.fr".</summary>
    public string AllowedOrigins { get; init; } = "";

    /// <summary>
    /// Comma separated list of debug origins, for example "http://localhost:8100". Empty or unset
    /// means no debug origin.
    /// </summary>
    public string DebugOrigins { get; init; } = "";

    /// <summary>
    /// Configured origins: trimmed, empty entries dropped, one trailing "/" removed, duplicates
    /// removed (case insensitive, first spelling kept).
    /// </summary>
    public IReadOnlyList<string> OriginList => ParseOrigins(AllowedOrigins);

    /// <summary>Debug origins, normalized exactly like <see cref="OriginList"/>.</summary>
    public IReadOnlyList<string> DebugOriginList => ParseOrigins(DebugOrigins);

    private static List<string> ParseOrigins(string? raw)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
            return result;
        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var origin = entry.EndsWith('/') ? entry[..^1] : entry;
            if (origin.Length > 0 && seen.Add(origin))
                result.Add(origin);
        }
        return result;
    }

    /// <summary>True when the origin points to the local machine (host "localhost").</summary>
    public static bool IsLocalhostOrigin(string origin)
        => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
           && string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns a failure message (never containing a value) or null when valid.</summary>
    public string? Validate(bool isDevelopment)
    {
        var origins = OriginList;
        if (!isDevelopment && origins.Count == 0)
            return "Cors:AllowedOrigins must contain at least one origin outside Development.";

        for (var i = 0; i < origins.Count; i++)
        {
            var item = origins[i];
            var label = $"Cors:AllowedOrigins item #{i + 1}";
            var syntax = CheckOriginSyntax(item, label, out var uri);
            if (syntax is not null)
                return syntax;
            if (!isDevelopment && !string.Equals(uri!.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return $"{label} must use https outside Development.";
            if (!isDevelopment && IsLocalhostOrigin(item))
                return $"{label} must not use the host localhost outside Development, use Cors:DebugOrigins.";
        }

        return ValidateDebugOrigins();
    }

    private string? ValidateDebugOrigins()
    {
        var debug = DebugOriginList;
        if (debug.Count > MaxDebugOrigins)
            return $"Cors:DebugOrigins must contain at most {MaxDebugOrigins} origins.";

        for (var i = 0; i < debug.Count; i++)
        {
            var item = debug[i];
            var label = $"Cors:DebugOrigins item #{i + 1}";
            var syntax = CheckOriginSyntax(item, label, out var uri);
            if (syntax is not null)
                return syntax;
            if (!string.Equals(uri!.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return $"{label} must use http or https.";
            if (!string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                return $"{label} must use the host localhost.";
            if (uri.IsDefaultPort)
                return $"{label} must have an explicit non default port.";
        }
        return null;
    }

    /// <summary>
    /// Shared syntax rules (wildcard, absolute origin, credentials, path/query/fragment). Returns a
    /// failure message or null, and the parsed Uri when the item is an absolute origin.
    /// </summary>
    private static string? CheckOriginSyntax(string item, string label, out Uri? uri)
    {
        uri = null;
        if (item.Contains('*'))
            return $"{label} must not contain a wildcard.";
        if (!Uri.TryCreate(item, UriKind.Absolute, out var parsed) || string.IsNullOrEmpty(parsed.Host)
            || !item.Contains("://", StringComparison.Ordinal))
            return $"{label} is not an absolute origin (scheme://host[:port]).";
        uri = parsed;
        if (!string.IsNullOrEmpty(parsed.UserInfo))
            return $"{label} must not contain credentials.";
        if (!string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment)
            || (parsed.AbsolutePath != "/" && parsed.AbsolutePath.Length > 0)
            || item.IndexOf('/', item.IndexOf("://", StringComparison.Ordinal) + 3) >= 0)
            return $"{label} must not contain a path, query or fragment.";
        return null;
    }
}
