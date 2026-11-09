using Mentora.Admin.Api;

namespace Mentora.Admin.Services;

/// <summary>
/// Listes fermées de l'écran Exercices, chargées une seule fois par session (service scoped).
/// Un échec n'est pas gardé : l'appel suivant réessaie.
/// </summary>
public sealed class ReferencesCache(ExercisesApi api)
{
    private Task<ApiResult<ExerciseReferences>>? _pending;

    public ExerciseReferences? Value { get; private set; }

    public async Task<ApiResult<ExerciseReferences>> GetAsync()
    {
        if (Value is not null)
            return ApiResult<ExerciseReferences>.Ok(200, Value);

        var task = _pending ??= LoadAsync();
        return await task;
    }

    private async Task<ApiResult<ExerciseReferences>> LoadAsync()
    {
        try
        {
            var result = await api.GetReferencesAsync();
            if (result.IsSuccess && result.Data is not null)
            {
                Value = result.Data;
                return result;
            }
            return result.IsSuccess ? ApiResult<ExerciseReferences>.Fail(0, null) : result;
        }
        finally
        {
            _pending = null;
        }
    }
}
