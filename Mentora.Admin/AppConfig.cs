namespace Mentora.Admin;

/// <summary>Configuration publique de l'admin (wwwroot/appsettings*.json). Aucun secret.</summary>
public sealed class AppConfig
{
    public AppConfig(string? apiBaseUrl, string? environmentLabel)
    {
        EnvironmentLabel = (environmentLabel ?? "").Trim();

        var raw = (apiBaseUrl ?? "").Trim();
        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            // Une base sans barre finale accepte les chemins relatifs "api/v1/...".
            ApiBaseUri = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
        }
    }

    public Uri? ApiBaseUri { get; }

    public string EnvironmentLabel { get; }

    public bool IsValid => ApiBaseUri is not null;
}
