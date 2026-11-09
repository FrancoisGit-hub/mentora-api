namespace Mentora.Admin.Api;

/// <summary>
/// Appels API authentifiés (routes hors /auth) : Bearer, rafraîchissement avant expiration,
/// un seul rafraîchissement puis un seul nouvel essai sur un 401.
/// </summary>
public sealed class ApiClient(ApiTransport transport, AdminSession session)
{
    public async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body = null,
        CancellationToken ct = default)
    {
        var (token, outcome) = await session.GetAccessTokenAsync();
        if (token is null)
            return outcome == RefreshOutcome.Unavailable
                ? ApiResult<T>.Fail(0, null)
                : ApiResult<T>.Fail(401, null);

        var result = await transport.SendAsync<T>(method, path, body, token, ct);
        if (!result.IsUnauthorized)
            return result;

        var refreshed = await session.RefreshAfterUnauthorizedAsync(token);
        if (refreshed == RefreshOutcome.Unavailable)
            return ApiResult<T>.Fail(0, null);
        if (refreshed == RefreshOutcome.Rejected || session.AccessToken is null)
            return result;

        result = await transport.SendAsync<T>(method, path, body, session.AccessToken, ct);
        if (result.IsUnauthorized)
            await session.EndSessionAsync(AdminSession.ExpiredMessage);
        return result;
    }

    public Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Get, path, null, ct);
}
