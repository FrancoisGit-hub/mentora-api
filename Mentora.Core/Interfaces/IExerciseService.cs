using Mentora.Core.DTOs.Exercise;

namespace Mentora.Core.Interfaces;

public interface IExerciseService
{
    Task<List<ExerciseResponse>> ListForCoachAsync(
        Guid coachId, string? search, string? muscleGroup, string? equipment,
        string scope, bool includeInactive, CancellationToken ct);

    Task<ExerciseResponse> GetByIdForCoachAsync(Guid exerciseId, Guid coachId, CancellationToken ct);

    Task<ExerciseResponse> CreateAsync(ExerciseRequest request, Guid coachId, CancellationToken ct);

    Task<ExerciseResponse> UpdateAsync(Guid exerciseId, ExerciseRequest request, Guid coachId, CancellationToken ct);

    Task DeleteAsync(Guid exerciseId, Guid coachId, CancellationToken ct);

    Task<List<ExerciseResponse>> ListForMemberAsync(Guid memberId, CancellationToken ct);

    Task<ExerciseResponse> GetByIdForMemberAsync(Guid exerciseId, Guid memberId, CancellationToken ct);

    /// <summary>
    /// Resolves, in a single query, which of the given exercise ids are visible to the coach
    /// (Mentora catalogue or owned by the coach) and currently active.
    /// </summary>
    Task<HashSet<Guid>> ResolveVisibleActiveIdsAsync(
        IReadOnlyCollection<Guid> exerciseIds, Guid coachId, CancellationToken ct);
}
