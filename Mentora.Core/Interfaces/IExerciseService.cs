using Mentora.Core.DTOs.Exercise;

namespace Mentora.Core.Interfaces;

public interface IExerciseService
{
    Task<List<ExerciseResponse>> ListForCoachAsync(
        Guid coachId, string? search, string? muscleGroup, string? equipment,
        Guid? muscleGroupId, Guid? equipmentId, Guid? objectiveId, string? level,
        string scope, bool includeInactive, CancellationToken ct);

    Task<ExerciseResponse> GetByIdForCoachAsync(Guid exerciseId, Guid coachId, CancellationToken ct);

    Task<ExerciseResponse> CreateAsync(ExerciseRequest request, Guid coachId, CancellationToken ct);

    Task<ExerciseResponse> UpdateAsync(Guid exerciseId, ExerciseRequest request, Guid coachId, CancellationToken ct);

    Task DeleteAsync(Guid exerciseId, Guid coachId, CancellationToken ct);

    /// <summary>
    /// Cursor-paginated light list of the active exercises visible to the member
    /// (Mentora catalogue plus exercises of the coaches the member is attached to).
    /// </summary>
    Task<ExerciseListResponseDto> ListForMemberAsync(
        Guid memberId, Guid? muscleGroupId, Guid? equipmentId, Guid? objectiveId, string? level,
        string? search, int limit, string? cursor, CancellationToken ct);

    Task<ExerciseResponse> GetByIdForMemberAsync(Guid exerciseId, Guid memberId, CancellationToken ct);

    /// <summary>Filter values used by at least one exercise visible to the member.</summary>
    Task<ExerciseFiltersResponse> GetFiltersForMemberAsync(Guid memberId, CancellationToken ct);

    /// <summary>Filter values used by at least one active exercise visible to the coach.</summary>
    Task<ExerciseFiltersResponse> GetFiltersForCoachAsync(Guid coachId, CancellationToken ct);

    /// <summary>
    /// Resolves, in a single query, which of the given exercise ids are visible to the coach
    /// (Mentora catalogue or owned by the coach) and currently active.
    /// </summary>
    Task<HashSet<Guid>> ResolveVisibleActiveIdsAsync(
        IReadOnlyCollection<Guid> exerciseIds, Guid coachId, CancellationToken ct);
}
