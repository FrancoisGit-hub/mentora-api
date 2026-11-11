namespace Mentora.Admin.Api;

/// <summary>
/// Appels de l'écran Exercices et vidéos. Tout passe par ApiClient (jeton, rafraîchissement,
/// un seul nouvel essai sur 401). Rien n'est journalisé.
/// </summary>
public sealed class ExercisesApi(ApiClient client)
{
    private const string Root = "api/v1/admin/";

    public Task<ApiResult<ExerciseReferences>> GetReferencesAsync(CancellationToken ct = default) =>
        client.GetAsync<ExerciseReferences>(Root + "exercise-references", ct);

    public Task<ApiResult<ExerciseListPage>> ListAsync(
        ExerciseFilter filter, string? cursor, int limit = 50, CancellationToken ct = default)
    {
        var query = new List<string> { "limit=" + limit };

        void Add(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
                query.Add(key + "=" + Uri.EscapeDataString(value));
        }

        Add("search", filter.Search);
        Add("categoryId", filter.CategoryId?.ToString());
        Add("muscleGroupId", filter.MuscleGroupId?.ToString());
        Add("level", filter.Level);
        if (filter.IsActive is { } active)
            Add("isActive", active ? "true" : "false");
        if (filter.HasLegacyValues == true)
            Add("hasLegacyValues", "true");
        Add("cursor", cursor);

        return client.GetAsync<ExerciseListPage>(Root + "exercises?" + string.Join("&", query), ct);
    }

    public Task<ApiResult<ExerciseDetail>> GetAsync(Guid exerciseId, CancellationToken ct = default) =>
        client.GetAsync<ExerciseDetail>(Root + "exercises/" + exerciseId, ct);

    public Task<ApiResult<ExerciseDetail>> CreateAsync(ExerciseRequest request, CancellationToken ct = default) =>
        client.SendAsync<ExerciseDetail>(HttpMethod.Post, Root + "exercises", request, ct);

    public Task<ApiResult<ExerciseDetail>> UpdateAsync(
        Guid exerciseId, ExerciseRequest request, CancellationToken ct = default) =>
        client.SendAsync<ExerciseDetail>(HttpMethod.Put, Root + "exercises/" + exerciseId, request, ct);

    public Task<ApiResult<ExerciseDetail>> SetActivationAsync(
        Guid exerciseId, bool isActive, CancellationToken ct = default) =>
        client.SendAsync<ExerciseDetail>(
            HttpMethod.Put, Root + "exercises/" + exerciseId + "/activation", new ActivationBody(isActive), ct);

    // Vidéo MUX (lot 12.3a). Aucun corps. L'adresse d'envoi de la réponse n'est jamais journalisée.

    public Task<ApiResult<VideoUploadResponse>> CreateVideoUploadAsync(Guid exerciseId, CancellationToken ct = default) =>
        client.SendAsync<VideoUploadResponse>(HttpMethod.Post, Root + "exercises/" + exerciseId + "/video-uploads", null, ct);

    public Task<ApiResult<VideoSyncResponse>> SyncVideoAsync(Guid exerciseId, CancellationToken ct = default) =>
        client.SendAsync<VideoSyncResponse>(HttpMethod.Post, Root + "exercises/" + exerciseId + "/video/sync", null, ct);

    /// <summary>200 avec data null : le succès se lit sur IsSuccess.</summary>
    public Task<ApiResult<object>> RemoveVideoAsync(Guid exerciseId, CancellationToken ct = default) =>
        client.SendAsync<object>(HttpMethod.Delete, Root + "exercises/" + exerciseId + "/video", null, ct);
}
