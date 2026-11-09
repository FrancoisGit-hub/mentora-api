namespace Mentora.Core.Options;

/// <summary>
/// Lot 12.5a. Allowed browser origins, from the environment variable Cors__AllowedOrigins (comma
/// separated). In Development the local origins are added by Program.cs. Failure messages never
/// contain a value.
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Comma separated list of origins, for example "https://admin.mentorapp.fr".</summary>
    public string AllowedOrigins { get; init; } = "";

    /// <summary>
    /// Configured origins: trimmed, empty entries dropped, one trailing "/" removed, duplicates
    /// removed (case insensitive, first spelling kept).
    /// </summary>
    public IReadOnlyList<string> OriginList
    {
        get
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(AllowedOrigins))
                return result;
            foreach (var raw in AllowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var origin = raw.EndsWith('/') ? raw[..^1] : raw;
                if (origin.Length > 0 && seen.Add(origin))
                    result.Add(origin);
            }
            return result;
        }
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
            if (item.Contains('*'))
                return $"{label} must not contain a wildcard.";
            if (!Uri.TryCreate(item, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)
                || !item.Contains("://", StringComparison.Ordinal))
                return $"{label} is not an absolute origin (scheme://host[:port]).";
            if (!string.IsNullOrEmpty(uri.UserInfo))
                return $"{label} must not contain credentials.";
            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
                || (uri.AbsolutePath != "/" && uri.AbsolutePath.Length > 0)
                || item.IndexOf('/', item.IndexOf("://", StringComparison.Ordinal) + 3) >= 0)
                return $"{label} must not contain a path, query or fragment.";
            if (!isDevelopment && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return $"{label} must use https outside Development.";
        }
        return null;
    }
}
