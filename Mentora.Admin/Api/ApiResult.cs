using System.Text.Json;

namespace Mentora.Admin.Api;

/// <summary>
/// Résultat d'un appel API après lecture de l'enveloppe { success, data, error, statusCode }.
/// Status vaut 0 quand le serveur n'a pas répondu (réseau, CORS, coupure).
/// </summary>
public sealed record ApiResult<T>(
    bool IsSuccess,
    int Status,
    T? Data,
    string? Error,
    JsonElement? Details,
    IReadOnlyDictionary<string, string[]>? Errors)
{
    public bool IsNetworkError => Status == 0;

    public bool IsServerError => Status >= 500;

    public bool IsUnauthorized => Status == 401;

    public static ApiResult<T> Ok(int status, T? data) => new(true, status, data, null, null, null);

    public static ApiResult<T> Fail(
        int status,
        string? error,
        JsonElement? details = null,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(false, status, default, error, details, errors);
}
