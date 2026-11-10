using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mentora.Core.Exceptions;
using Mentora.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services.Mux;

/// <summary>Lot 12.3a — MUX Video REST calls used by the admin video routes (no SDK).</summary>
public interface IMuxClient
{
    /// <summary>POST /video/v1/uploads. Public playback, no static rendition.</summary>
    Task<MuxDirectUpload> CreateDirectUploadAsync(string passthrough, string title, string externalId, CancellationToken ct);

    /// <summary>GET /video/v1/uploads/{id}. Null when MUX answers 404.</summary>
    Task<MuxUpload?> GetUploadAsync(string uploadId, CancellationToken ct);

    /// <summary>PUT /video/v1/uploads/{id}/cancel. False when MUX answers 404 (already gone).</summary>
    Task<bool> CancelUploadAsync(string uploadId, CancellationToken ct);

    /// <summary>GET /video/v1/assets/{id}. Null when MUX answers 404.</summary>
    Task<MuxAsset?> GetAssetAsync(string assetId, CancellationToken ct);

    /// <summary>DELETE /video/v1/assets/{id}. False when MUX answers 404 (already gone).</summary>
    Task<bool> DeleteAssetAsync(string assetId, CancellationToken ct);
}

/// <summary>
/// Typed HttpClient (BaseAddress = Mux:ApiBaseUrl, 15 s timeout, set at registration). HTTP Basic is added
/// per request from the options. Any non-2xx other than the documented 404s, a timeout or a network error
/// throws <see cref="MuxUnavailableException"/>. Logs carry the operation, the MUX status code and the MUX
/// request id only: never a URL, a response body or a credential.
/// </summary>
public sealed class MuxClient(HttpClient http, IOptions<MuxOptions> options, ILogger<MuxClient> logger) : IMuxClient
{
    public const int MaxTitleCodePoints = 512;
    public const int MaxExternalIdCodePoints = 128;

    private MuxOptions Options => options.Value;

    public async Task<MuxDirectUpload> CreateDirectUploadAsync(
        string passthrough, string title, string externalId, CancellationToken ct)
    {
        var body = new
        {
            cors_origin = Options.NormalizedCorsOrigin,
            timeout = Options.UploadTimeoutSeconds,
            new_asset_settings = new
            {
                playback_policies = new[] { "public" },
                video_quality = Options.VideoQuality,
                passthrough,
                meta = new
                {
                    title = MuxJson.TruncateCodePoints(title, MaxTitleCodePoints),
                    external_id = MuxJson.TruncateCodePoints(externalId, MaxExternalIdCodePoints),
                },
            },
        };

        using var request = NewRequest(HttpMethod.Post, "video/v1/uploads");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var doc = await SendAsync(request, "create upload", allowNotFound: false, ct)
                        ?? throw Unavailable("create upload");
        var data = doc.RootElement.TryGetProperty("data", out var d) ? d : default;
        var id = MuxJson.Str(data, "id");
        var url = MuxJson.Str(data, "url");
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(url))
        {
            logger.LogWarning("[MUX] create upload answered without id or url");
            throw Unavailable("create upload");
        }
        int? timeout = data.TryGetProperty("timeout", out var t) && t.TryGetInt32(out var tv) ? tv : null;
        return new MuxDirectUpload(id, url, timeout);
    }

    public async Task<MuxUpload?> GetUploadAsync(string uploadId, CancellationToken ct)
    {
        using var request = NewRequest(HttpMethod.Get, $"video/v1/uploads/{Uri.EscapeDataString(uploadId)}");
        using var doc = await SendAsync(request, "get upload", allowNotFound: true, ct);
        if (doc is null) return null;
        return doc.RootElement.TryGetProperty("data", out var data) ? MuxJson.ParseUpload(data) : null;
    }

    public async Task<bool> CancelUploadAsync(string uploadId, CancellationToken ct)
    {
        using var request = NewRequest(HttpMethod.Put, $"video/v1/uploads/{Uri.EscapeDataString(uploadId)}/cancel");
        using var doc = await SendAsync(request, "cancel upload", allowNotFound: true, ct);
        return doc is not null;
    }

    public async Task<MuxAsset?> GetAssetAsync(string assetId, CancellationToken ct)
    {
        using var request = NewRequest(HttpMethod.Get, $"video/v1/assets/{Uri.EscapeDataString(assetId)}");
        using var doc = await SendAsync(request, "get asset", allowNotFound: true, ct);
        if (doc is null) return null;
        return doc.RootElement.TryGetProperty("data", out var data) ? MuxJson.ParseAsset(data) : null;
    }

    public async Task<bool> DeleteAssetAsync(string assetId, CancellationToken ct)
    {
        using var request = NewRequest(HttpMethod.Delete, $"video/v1/assets/{Uri.EscapeDataString(assetId)}");
        using var doc = await SendAsync(request, "delete asset", allowNotFound: true, ct);
        return doc is not null;
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string relativePath)
    {
        var request = new HttpRequestMessage(method, relativePath);
        var raw = Encoding.UTF8.GetBytes(Options.TokenId + ":" + Options.TokenSecret);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    /// <summary>
    /// Sends the request. Returns the parsed body on 2xx (an empty JSON object when the body is empty), null on
    /// 404 when allowed, throws <see cref="MuxUnavailableException"/> otherwise.
    /// </summary>
    private async Task<JsonDocument?> SendAsync(
        HttpRequestMessage request, string operation, bool allowNotFound, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("[MUX] {Operation} timed out", operation);
            throw Unavailable(operation);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("[MUX] {Operation} network error {Error}", operation, ex.HttpRequestError);
            throw Unavailable(operation);
        }

        using (response)
        {
            var requestId = response.Headers.TryGetValues("x-request-id", out var values)
                ? values.FirstOrDefault()
                : null;

            if (response.StatusCode == HttpStatusCode.NotFound && allowNotFound)
            {
                logger.LogInformation("[MUX] {Operation} status=404 (already gone) requestId={RequestId}",
                    operation, requestId ?? "none");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("[MUX] {Operation} failed status={Status} requestId={RequestId}",
                    operation, (int)response.StatusCode, requestId ?? "none");
                throw Unavailable(operation);
            }

            try
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                return bytes.Length == 0 ? JsonDocument.Parse("{}") : JsonDocument.Parse(bytes);
            }
            catch (JsonException)
            {
                logger.LogWarning("[MUX] {Operation} answered status={Status} with a body that is not JSON requestId={RequestId}",
                    operation, (int)response.StatusCode, requestId ?? "none");
                throw Unavailable(operation);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("[MUX] {Operation} timed out while reading the answer", operation);
                throw Unavailable(operation);
            }
        }
    }

    private static MuxUnavailableException Unavailable(string operation) =>
        new($"MUX {operation} failed.");
}
