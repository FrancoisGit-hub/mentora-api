namespace Mentora.Admin.Api;

/// <summary>
/// Appels de l'écran Coachs. Tout passe par ApiClient (jeton, rafraîchissement,
/// un seul nouvel essai sur 401). Rien n'est journalisé.
/// </summary>
public sealed class CoachesApi(ApiClient client)
{
    private const string Root = "api/v1/admin/";

    public Task<ApiResult<CoachListPage>> ListAsync(
        string? cursor, int limit = 50, CancellationToken ct = default)
    {
        var query = "limit=" + limit;
        if (!string.IsNullOrEmpty(cursor))
            query += "&cursor=" + Uri.EscapeDataString(cursor);

        return client.GetAsync<CoachListPage>(Root + "coaches?" + query, ct);
    }

    public Task<ApiResult<CoachListItem>> CreateAsync(CreateCoachRequest request, CancellationToken ct = default) =>
        client.SendAsync<CoachListItem>(HttpMethod.Post, Root + "coaches", request, ct);
}
