using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using Mentora.Core.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mentora.Infrastructure.Services.Push;

public enum FcmOutcome
{
    Success,

    /// <summary>FCM UNREGISTERED (404) or INVALID_ARGUMENT (400): the device row must be deleted.</summary>
    InvalidToken,

    /// <summary>429, 5xx, timeout or network error.</summary>
    Retryable,

    /// <summary>Any other error.</summary>
    Failed,
}

/// <summary>Outcome of one send. ErrorCode is the FCM error code (or OAUTH), never a body or a token.</summary>
/// <param name="Field">Name of the rejected field when FCM refused our payload (INVALID_ARGUMENT naming a field other than message.token).</param>
public sealed record FcmSendResult(FcmOutcome Outcome, int? HttpCode, string? ErrorCode, string? Field = null);

public interface IFcmClient
{
    bool IsConfigured { get; }

    Task<FcmSendResult> SendAsync(
        string deviceToken, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken ct);
}

/// <summary>
/// Lot 14.9b. Process-wide state of the Google OAuth2 access token: the service account (loaded once, lazily),
/// the cached token and its expiry. Singleton. Nothing here is ever logged.
/// </summary>
public sealed class FcmAuthState(IOptions<FirebaseOptions> options) : IDisposable
{
    internal const string TokenUri = "https://oauth2.googleapis.com/token";
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly object _accountLock = new();
    private FirebaseServiceAccount? _account;
    private string? _accessToken;
    private DateTime _expiresUtc = DateTime.MinValue;

    public void Invalidate()
    {
        _accessToken = null;
        _expiresUtc = DateTime.MinValue;
    }

    /// <summary>Returns the cached token, or fetches one. On failure the token is null and Failure says why.</summary>
    internal async Task<(string? Token, FcmSendResult? Failure)> GetAccessTokenAsync(HttpClient http, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_accessToken is not null && DateTime.UtcNow < _expiresUtc - RefreshMargin)
                return (_accessToken, null);

            var account = GetAccount();
            if (account is null)
                return (null, new FcmSendResult(FcmOutcome.Failed, null, "NOT_CONFIGURED"));

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUri)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = BuildAssertion(account),
                }),
            };
            using var response = await http.SendAsync(request, ct);
            var code = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                var retryable = response.StatusCode == HttpStatusCode.TooManyRequests || code >= 500;
                return (null, new FcmSendResult(retryable ? FcmOutcome.Retryable : FcmOutcome.Failed, code, "OAUTH"));
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
            var token = doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var s) ? s : 3600;
            if (string.IsNullOrEmpty(token))
                return (null, new FcmSendResult(FcmOutcome.Failed, code, "OAUTH"));

            _accessToken = token;
            _expiresUtc = DateTime.UtcNow.AddSeconds(expiresIn);
            return (token, null);
        }
        finally
        {
            _lock.Release();
        }
    }

    private FirebaseServiceAccount? GetAccount()
    {
        lock (_accountLock)
        {
            if (_account is not null)
                return _account;
            var o = options.Value;
            if (!o.IsConfigured)
                return null;
            FirebaseServiceAccount.TryLoad(o.ServiceAccountPath, o.ProjectId, out _account);
            return _account;
        }
    }

    /// <summary>RS256 assertion: iss = client_email, scope, aud = token endpoint, iat = now, exp = now + 3600 s.</summary>
    private static string BuildAssertion(FirebaseServiceAccount account)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = account.ClientEmail,
            Audience = TokenUri,
            IssuedAt = now,
            Expires = now.AddSeconds(3600),
            Claims = new Dictionary<string, object> { ["scope"] = Scope },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(account.Key), SecurityAlgorithms.RsaSha256),
        };
        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    public void Dispose()
    {
        _account?.Dispose();
        _lock.Dispose();
    }
}

/// <summary>
/// Lot 14.9b. FCM HTTP v1 without SDK (typed HttpClient, 10 s timeout set at registration). Logs nothing: the
/// caller logs ids, HTTP codes and FCM error codes only. The response body is parsed for error.status and
/// error.details[].errorCode and never kept.
/// </summary>
public sealed class FcmClient(HttpClient http, FcmAuthState auth, IOptions<FirebaseOptions> options) : IFcmClient
{
    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<FcmSendResult> SendAsync(
        string deviceToken, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        try
        {
            // A 401 clears the cached access token once and retries this device once.
            for (var pass = 0; pass < 2; pass++)
            {
                var (accessToken, failure) = await auth.GetAccessTokenAsync(http, ct);
                if (accessToken is null)
                    return failure!;

                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(options.Value.ProjectId)}/messages:send")
                {
                    Content = new StringContent(BuildPayload(deviceToken, title, body, data), Encoding.UTF8, "application/json"),
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                using var response = await http.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.Unauthorized && pass == 0)
                {
                    auth.Invalidate();
                    continue;
                }
                return await InterpretAsync(response, ct);
            }
            return new FcmSendResult(FcmOutcome.Failed, 401, null);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FcmSendResult(FcmOutcome.Retryable, null, "TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return new FcmSendResult(FcmOutcome.Retryable, null, "NETWORK");
        }
        catch (JsonException)
        {
            return new FcmSendResult(FcmOutcome.Failed, null, "OAUTH");
        }
    }

    private static string BuildPayload(string token, string title, string body, IReadOnlyDictionary<string, string> data) =>
        JsonSerializer.Serialize(new
        {
            message = new
            {
                token,
                notification = new { title, body },
                data,
                android = new { priority = "HIGH", notification = new { sound = "default" } },
                apns = new { payload = new { aps = new { sound = "default" } } },
            },
        });

    private static async Task<FcmSendResult> InterpretAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var code = (int)response.StatusCode;
        if (response.IsSuccessStatusCode)
            return new FcmSendResult(FcmOutcome.Success, code, null);

        var (errorCode, fields) = await ReadErrorAsync(response, ct);

        // Device removal decision. Delete the USER_DEVICES row only for UNREGISTERED, or for INVALID_ARGUMENT that
        // either names the field "message.token" in its BadRequest fieldViolations or names no field at all.
        if (code == 404 && errorCode == "UNREGISTERED")
            return new FcmSendResult(FcmOutcome.InvalidToken, code, errorCode);
        if (code == 400 && errorCode == "INVALID_ARGUMENT")
        {
            if (fields.Count == 0 || fields.Contains("message.token"))
                return new FcmSendResult(FcmOutcome.InvalidToken, code, errorCode);

            // Payload problem (only other fields are named): the device is fine, never delete it.
            return new FcmSendResult(FcmOutcome.Failed, code, errorCode, fields[0]);
        }
        if (response.StatusCode == HttpStatusCode.TooManyRequests || code >= 500)
            return new FcmSendResult(FcmOutcome.Retryable, code, errorCode);
        return new FcmSendResult(FcmOutcome.Failed, code, errorCode);
    }

    /// <summary>
    /// error.details[].errorCode when present, else error.status (null when neither), plus the field names of
    /// every google.rpc.BadRequest fieldViolation. The body itself is never kept or logged.
    /// </summary>
    private static async Task<(string? ErrorCode, List<string> Fields)> ReadErrorAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        var fields = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
            if (!doc.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
                return (null, fields);

            string? detailCode = null;
            if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
            {
                foreach (var detail in details.EnumerateArray())
                {
                    if (detail.ValueKind != JsonValueKind.Object)
                        continue;
                    if (detailCode is null
                        && detail.TryGetProperty("errorCode", out var c) && c.ValueKind == JsonValueKind.String)
                        detailCode = c.GetString();
                    if (detail.TryGetProperty("fieldViolations", out var violations) && violations.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var violation in violations.EnumerateArray())
                        {
                            if (violation.ValueKind == JsonValueKind.Object
                                && violation.TryGetProperty("field", out var f) && f.ValueKind == JsonValueKind.String
                                && !string.IsNullOrEmpty(f.GetString()))
                                fields.Add(f.GetString()!);
                        }
                    }
                }
            }

            var status = error.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
            return (detailCode ?? status, fields);
        }
        catch (JsonException)
        {
            return (null, fields);
        }
    }
}
