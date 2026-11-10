namespace Mentora.Core.Options;

/// <summary>
/// Lot 12.3a. MUX video settings, from the environment variables Mux__* only (no "Mux" section in
/// appsettings.json). Outside Development the four secrets/origin are required; in Development they are
/// either all empty (MUX disabled: the admin video routes and the webhook answer 503) or all valid.
/// Failure messages never contain a value.
/// </summary>
public sealed class MuxOptions
{
    public const string SectionName = "Mux";

    public const int MinWebhookSecretLength = 32;
    public const int MinUploadTimeoutSeconds = 60;
    public const int MaxUploadTimeoutSeconds = 604800;
    public const int MinWebhookToleranceSeconds = 60;
    public const int MaxWebhookToleranceSeconds = 900;

    private static readonly string[] VideoQualities = ["basic", "plus", "premium"];

    /// <summary>MUX access token id (HTTP Basic user).</summary>
    public string TokenId { get; init; } = "";

    /// <summary>MUX access token secret (HTTP Basic password).</summary>
    public string TokenSecret { get; init; } = "";

    /// <summary>MUX webhook signing secret (HMAC SHA256 key).</summary>
    public string WebhookSecret { get; init; } = "";

    /// <summary>Origin of the admin site, sent as cors_origin of every direct upload.</summary>
    public string CorsOrigin { get; init; } = "";

    public string ApiBaseUrl { get; init; } = "https://api.mux.com";

    public int UploadTimeoutSeconds { get; init; } = 3600;

    public int WebhookToleranceSeconds { get; init; } = 300;

    public string VideoQuality { get; init; } = "basic";

    /// <summary>True when the four required values are set.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TokenId)
        && !string.IsNullOrWhiteSpace(TokenSecret)
        && !string.IsNullOrWhiteSpace(WebhookSecret)
        && !string.IsNullOrWhiteSpace(CorsOrigin);

    /// <summary>CorsOrigin without a trailing "/" (MUX compares origins exactly).</summary>
    public string NormalizedCorsOrigin
    {
        get
        {
            var origin = CorsOrigin.Trim();
            return origin.EndsWith('/') ? origin[..^1] : origin;
        }
    }

    /// <summary>Returns a failure message (never containing a value) or null when valid.</summary>
    public string? Validate(bool isDevelopment)
    {
        if (UploadTimeoutSeconds is < MinUploadTimeoutSeconds or > MaxUploadTimeoutSeconds)
            return $"Mux:UploadTimeoutSeconds must be between {MinUploadTimeoutSeconds} and {MaxUploadTimeoutSeconds}.";
        if (WebhookToleranceSeconds is < MinWebhookToleranceSeconds or > MaxWebhookToleranceSeconds)
            return $"Mux:WebhookToleranceSeconds must be between {MinWebhookToleranceSeconds} and {MaxWebhookToleranceSeconds}.";
        if (!VideoQualities.Contains(VideoQuality, StringComparer.Ordinal))
            return "Mux:VideoQuality must be one of basic, plus, premium.";
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var apiBase)
            || (apiBase.Scheme != Uri.UriSchemeHttps && !(isDevelopment && apiBase.Scheme == Uri.UriSchemeHttp))
            || !string.IsNullOrEmpty(apiBase.UserInfo))
            return "Mux:ApiBaseUrl must be an absolute https URL without credentials.";

        var required = new (string Name, string Value)[]
        {
            ("Mux:TokenId", TokenId),
            ("Mux:TokenSecret", TokenSecret),
            ("Mux:WebhookSecret", WebhookSecret),
            ("Mux:CorsOrigin", CorsOrigin),
        };
        var setCount = required.Count(r => !string.IsNullOrWhiteSpace(r.Value));

        if (isDevelopment && setCount == 0)
            return null; // MUX disabled locally.

        if (isDevelopment && setCount < required.Length)
            return "Mux settings are partial: in Development set Mux:TokenId, Mux:TokenSecret, Mux:WebhookSecret and Mux:CorsOrigin together, or none of them.";

        foreach (var (name, value) in required)
            if (string.IsNullOrWhiteSpace(value))
                return $"{name} must be set as an environment variable outside Development.";

        if (!isDevelopment && WebhookSecret.Length < MinWebhookSecretLength)
            return $"Mux:WebhookSecret must be at least {MinWebhookSecretLength} characters.";

        return ValidateCorsOrigin(isDevelopment);
    }

    private string? ValidateCorsOrigin(bool isDevelopment)
    {
        var origin = NormalizedCorsOrigin;
        if (origin.Contains('*') || origin.Contains(','))
            return "Mux:CorsOrigin must be a single origin without wildcard.";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)
            || !origin.Contains("://", StringComparison.Ordinal))
            return "Mux:CorsOrigin is not an absolute origin (scheme://host[:port]).";
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return "Mux:CorsOrigin must not contain credentials.";
        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || origin.IndexOf('/', origin.IndexOf("://", StringComparison.Ordinal) + 3) >= 0)
            return "Mux:CorsOrigin must not contain a path, query or fragment.";

        var isHttps = uri.Scheme == Uri.UriSchemeHttps;
        var isLocalHttp = uri.Scheme == Uri.UriSchemeHttp
                          && string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        if (isDevelopment ? !(isHttps || isLocalHttp) : !isHttps)
            return isDevelopment
                ? "Mux:CorsOrigin must use https, or http://localhost in Development."
                : "Mux:CorsOrigin must use https outside Development.";
        return null;
    }
}
