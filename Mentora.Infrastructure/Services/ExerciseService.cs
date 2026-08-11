using FluentValidation;
using Mentora.Core.DTOs.Exercise;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class ExerciseService(
    MentoraDbContext db,
    IValidator<ExerciseRequest> validator) : IExerciseService
{
    public async Task<List<ExerciseResponse>> ListForCoachAsync(
        Guid coachId, string? search, string? muscleGroup, string? equipment,
        string scope, bool includeInactive, CancellationToken ct)
    {
        ValidateScope(scope);
        var muscleGroupFilter = ParseMuscleGroupFilter(muscleGroup);
        var equipmentFilter   = ParseEquipmentFilter(equipment);

        // Read rule, applied to every read without exception: Mentora entries + this coach's own.
        var query = db.Exercises
            .Where(e => e.ExerciseCoachId == null || e.ExerciseCoachId == coachId);

        if (scope == "MENTORA")
            query = query.Where(e => e.ExerciseCoachId == null);
        else if (scope == "MINE")
            query = query.Where(e => e.ExerciseCoachId == coachId);

        if (!includeInactive)
            query = query.Where(e => e.ExerciseIsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => EF.Functions.ILike(e.ExerciseName, $"%{search.Trim()}%"));

        if (muscleGroupFilter is not null)
            query = query.Where(e => e.ExerciseMuscleGroup == muscleGroupFilter.Value);

        if (equipmentFilter is not null)
            query = query.Where(e => e.ExerciseEquipment == equipmentFilter.Value);

        return await query
            .OrderBy(e => e.ExerciseName)
            .Select(e => ToResponse(e))
            .ToListAsync(ct);
    }

    public async Task<ExerciseResponse> GetByIdForCoachAsync(Guid exerciseId, Guid coachId, CancellationToken ct)
    {
        var exercise = await db.Exercises
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId
                                    && (e.ExerciseCoachId == null || e.ExerciseCoachId == coachId), ct)
            ?? throw new NotFoundException($"Exercise {exerciseId} not found.");

        return ToResponse(exercise);
    }

    public async Task<ExerciseResponse> CreateAsync(ExerciseRequest request, Guid coachId, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var now = DateTime.UtcNow;
        var exercise = new Exercise
        {
            ExerciseCoachId         = coachId,
            ExerciseName            = request.Name.Trim(),
            ExerciseDescription     = request.Description?.Trim(),
            ExerciseInstructions    = request.Instructions?.Trim(),
            ExerciseVideoUrl        = request.VideoUrl?.Trim(),
            ExerciseImageUrl        = request.ImageUrl?.Trim(),
            ExerciseMuscleGroup     = EnumMappings.MuscleGroupMapping.Parse(request.MuscleGroup),
            ExerciseEquipment       = EnumMappings.EquipmentMapping.Parse(request.Equipment),
            ExerciseIsPolyarticular = request.IsPolyarticular,
            ExerciseIsActive        = true,
            ExerciseCreatedDate     = now,
            ExerciseUpdatedDate     = now,
        };

        db.Exercises.Add(exercise);
        await db.SaveChangesAsync(ct);

        return ToResponse(exercise);
    }

    public async Task<ExerciseResponse> UpdateAsync(
        Guid exerciseId, ExerciseRequest request, Guid coachId, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        // Write rule: only rows owned by this coach — a Mentora entry (CoachId IS NULL) or
        // another coach's row falls out of this predicate and 404s, never 403.
        var exercise = await db.Exercises
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId && e.ExerciseCoachId == coachId, ct)
            ?? throw new NotFoundException($"Exercise {exerciseId} not found.");

        exercise.ExerciseName            = request.Name.Trim();
        exercise.ExerciseDescription     = request.Description?.Trim();
        exercise.ExerciseInstructions    = request.Instructions?.Trim();
        exercise.ExerciseVideoUrl        = request.VideoUrl?.Trim();
        exercise.ExerciseImageUrl        = request.ImageUrl?.Trim();
        exercise.ExerciseMuscleGroup     = EnumMappings.MuscleGroupMapping.Parse(request.MuscleGroup);
        exercise.ExerciseEquipment       = EnumMappings.EquipmentMapping.Parse(request.Equipment);
        exercise.ExerciseIsPolyarticular = request.IsPolyarticular;
        exercise.ExerciseUpdatedDate     = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return ToResponse(exercise);
    }

    public async Task DeleteAsync(Guid exerciseId, Guid coachId, CancellationToken ct)
    {
        var exercise = await db.Exercises
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId && e.ExerciseCoachId == coachId, ct)
            ?? throw new NotFoundException($"Exercise {exerciseId} not found.");

        exercise.ExerciseIsActive    = false;
        exercise.ExerciseUpdatedDate = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ExerciseResponse>> ListForMemberAsync(Guid memberId, CancellationToken ct)
    {
        var coachIds = await db.MemberCoaches
            .Where(mc => mc.MemberId == memberId)
            .Select(mc => mc.CoachId)
            .ToListAsync(ct);

        return await db.Exercises
            .Where(e => e.ExerciseIsActive
                     && (e.ExerciseCoachId == null || coachIds.Contains(e.ExerciseCoachId!.Value)))
            .OrderBy(e => e.ExerciseName)
            .Select(e => ToResponse(e))
            .ToListAsync(ct);
    }

    public async Task<ExerciseResponse> GetByIdForMemberAsync(Guid exerciseId, Guid memberId, CancellationToken ct)
    {
        var coachIds = await db.MemberCoaches
            .Where(mc => mc.MemberId == memberId)
            .Select(mc => mc.CoachId)
            .ToListAsync(ct);

        var exercise = await db.Exercises
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId
                                    && e.ExerciseIsActive
                                    && (e.ExerciseCoachId == null || coachIds.Contains(e.ExerciseCoachId!.Value)), ct)
            ?? throw new NotFoundException($"Exercise {exerciseId} not found.");

        return ToResponse(exercise);
    }

    public async Task<HashSet<Guid>> ResolveVisibleActiveIdsAsync(
        IReadOnlyCollection<Guid> exerciseIds, Guid coachId, CancellationToken ct)
    {
        if (exerciseIds.Count == 0)
            return [];

        var visibleIds = await db.Exercises
            .Where(e => exerciseIds.Contains(e.ExerciseId)
                     && (e.ExerciseCoachId == null || e.ExerciseCoachId == coachId)
                     && e.ExerciseIsActive)
            .Select(e => e.ExerciseId)
            .ToListAsync(ct);

        return visibleIds.ToHashSet();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void ValidateScope(string scope)
    {
        if (scope != "ALL" && scope != "MENTORA" && scope != "MINE")
            throw new InvalidOperationException($"Invalid scope '{scope}'. Accepted values: ALL, MENTORA, MINE.");
    }

    private static MuscleGroup? ParseMuscleGroupFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim().ToUpperInvariant();
        if (!EnumMappings.MuscleGroupMapping.WireValues.Contains(normalized))
            throw new InvalidOperationException(
                $"Invalid muscleGroup '{value}'. Accepted values: {string.Join(", ", EnumMappings.MuscleGroupMapping.WireValues)}.");

        return EnumMappings.MuscleGroupMapping.Parse(normalized);
    }

    private static Equipment? ParseEquipmentFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim().ToUpperInvariant();
        if (!EnumMappings.EquipmentMapping.WireValues.Contains(normalized))
            throw new InvalidOperationException(
                $"Invalid equipment '{value}'. Accepted values: {string.Join(", ", EnumMappings.EquipmentMapping.WireValues)}.");

        return EnumMappings.EquipmentMapping.Parse(normalized);
    }

    private static ExerciseResponse ToResponse(Exercise e) => new(
        e.ExerciseId,
        e.ExerciseCoachId,
        e.ExerciseName,
        e.ExerciseDescription,
        e.ExerciseInstructions,
        e.ExerciseVideoUrl,
        e.ExerciseImageUrl,
        EnumMappings.MuscleGroupMapping.ToWire(e.ExerciseMuscleGroup),
        EnumMappings.EquipmentMapping.ToWire(e.ExerciseEquipment),
        e.ExerciseIsPolyarticular,
        e.ExerciseIsActive,
        e.ExerciseCreatedDate,
        e.ExerciseUpdatedDate
    );
}
