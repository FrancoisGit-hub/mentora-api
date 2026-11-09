using Mentora.Core.DTOs.AdminExercise;

namespace Mentora.Core.Interfaces;

/// <summary>Lot 12.2b — admin back-office: edition of the Mentora exercise catalog (EXERCISE_COACH_ID NULL only).</summary>
public interface IAdminExerciseService
{
    /// <summary>Reference lists the exercise form needs.</summary>
    Task<AdminExerciseReferencesResponse> GetReferencesAsync(CancellationToken ct);

    /// <summary>Mentora exercises, inactive included. Invalid limit, cursor, level or search throw InvalidOperationException (400).</summary>
    Task<AdminExerciseListResponse> ListAsync(
        string? search, bool? isActive, Guid? categoryId, Guid? muscleGroupId, Guid? equipmentId,
        Guid? objectiveId, string? level, bool? hasLegacyValues, int limit, string? cursor, CancellationToken ct);

    /// <summary>NotFoundException for an unknown id or a coach-owned exercise.</summary>
    Task<AdminExerciseResponse> GetByIdAsync(Guid exerciseId, CancellationToken ct);

    /// <summary>Creates a Mentora exercise (source code allocated by the service). ConflictException EXERCISE_NAME_TAKEN on a duplicate name.</summary>
    Task<AdminExerciseResponse> CreateAsync(AdminExerciseRequest request, Guid adminUserId, CancellationToken ct);

    /// <summary>Full replacement. No stored value changes: no write, no audit row.</summary>
    Task<AdminExerciseResponse> UpdateAsync(Guid exerciseId, AdminExerciseRequest request, Guid adminUserId, CancellationToken ct);

    /// <summary>Same state: no write. Deactivating a used exercise throws ConflictException EXERCISE_IN_USE.</summary>
    Task<AdminExerciseResponse> SetActivationAsync(Guid exerciseId, AdminExerciseActivationRequest request, Guid adminUserId, CancellationToken ct);
}
