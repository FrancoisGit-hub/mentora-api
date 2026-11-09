using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mentora.Admin.Api;

/// <summary>
/// Le seul HttpClient typé de l'admin. BaseAddress = ApiBaseUrl. Il ne connaît pas la session :
/// l'appelant passe le jeton. Il ne journalise rien (ni jeton, ni code, ni adresse).
/// </summary>
public sealed class ApiTransport(HttpClient http)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body = null,
        string? bearer = null,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        if (!string.IsNullOrEmpty(bearer))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        HttpResponseMessage response;
        string text;
        try
        {
            response = await http.SendAsync(request, ct);
            text = await response.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return ApiResult<T>.Fail(0, null);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            return Parse<T>(status, response.IsSuccessStatusCode, text);
        }
    }

    private static ApiResult<T> Parse<T>(int status, bool httpOk, string text)
    {
        JsonDocument? doc = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(text))
                doc = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            doc = null;
        }

        using (doc)
        {
            var root = doc?.RootElement;
            var isObject = root is { ValueKind: JsonValueKind.Object };

            if (httpOk)
            {
                T? data = default;
                if (isObject && root!.Value.TryGetProperty("data", out var d)
                    && d.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                {
                    try { data = d.Deserialize<T>(Json); }
                    catch (JsonException) { return ApiResult<T>.Fail(status, null); }
                }
                return ApiResult<T>.Ok(status, data);
            }

            string? error = null;
            JsonElement? details = null;
            Dictionary<string, string[]>? errors = null;

            if (isObject)
            {
                var r = root!.Value;
                if (r.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                    error = e.GetString();
                if (r.TryGetProperty("details", out var det) && det.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                    details = det.Clone();
                if (r.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Object)
                {
                    errors = new Dictionary<string, string[]>();
                    foreach (var p in errs.EnumerateObject())
                    {
                        errors[p.Name] = p.Value.ValueKind == JsonValueKind.Array
                            ? p.Value.EnumerateArray()
                                .Where(x => x.ValueKind == JsonValueKind.String)
                                .Select(x => x.GetString() ?? "")
                                .ToArray()
                            : [];
                    }
                }
            }

            return ApiResult<T>.Fail(status, error, details, errors);
        }
    }
}
