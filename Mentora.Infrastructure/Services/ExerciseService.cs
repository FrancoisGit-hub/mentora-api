using System.Text;
using System.Text.Json;
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
    private const int SearchMinLength = 2;
    private const int SearchMaxLength = 60;

    public async Task<List<ExerciseResponse>> ListForCoachAsync(
        Guid coachId, string? search, string? muscleGroup, string? equipment,
        Guid? muscleGroupId, Guid? equipmentId, Guid? objectiveId, string? level,
        string scope, bool includeInactive, CancellationToken ct)
    {
        ValidateScope(scope);
        var muscleGroupFilter = ParseMuscleGroupFilter(muscleGroup);
        var equipmentFilter   = ParseEquipmentFilter(equipment);
        var maxLevel          = ParseLevelFilter(level);

        // Read rule, applied to every read without exception: Mentora entries + this coach's own.
        var query = db.Exercises
            .Where(e => e.ExerciseCoachId == null || e.ExerciseCoachId == coachId);

        if (scope == "MENTORA")
            query = query.Where(e => e.ExerciseCoachId == null);
        else if (scope == "MINE")
            query = query.Where(e => e.ExerciseCoachId == coachId);

        if (!includeInactive)
            query = query.Where(e => e.ExerciseIsActive);

        if (muscleGroupFilter is not null)
            query = query.Where(e => e.ExerciseMuscleGroup == muscleGroupFilter.Value);

        if (equipmentFilter is not null)
            query = query.Where(e => e.ExerciseEquipment == equipmentFilter.Value);

        query = ApplyCatalogFilters(query, muscleGroupId, equipmentId, objectiveId, maxLevel);

        // Search matches the name and alternative names only and ranks by relevance (best first);
        // without a search the order is unchanged (by name).
        if (!string.IsNullOrWhiteSpace(search))
        {
            var ranked = await ExerciseSearch.WithRank(WithDetail(query).AsNoTracking(), search)
                .Where(r => r.Rank <= ExerciseSearch.MaxRank)
                .OrderBy(r => r.Rank)
                .ThenBy(r => r.Exercise.ExerciseName)
                .ThenBy(r => r.Exercise.ExerciseId)
                .AsSplitQuery()
                .ToListAsync(ct);

            return ranked.Select(r => ToResponse(r.Exercise)).ToList();
        }

        var exercises = await WithDetail(query)
            .AsNoTracking()
            .AsSplitQuery()
            .OrderBy(e => e.ExerciseName)
            .ToListAsync(ct);

        return exercises.Select(ToResponse).ToList();
    }

    public async Task<ExerciseResponse> GetByIdForCoachAsync(Guid exerciseId, Guid coachId, CancellationToken ct)
    {
        var exercise = await WithDetail(db.Exercises.AsNoTracking())
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
        // Write rule: only rows owned by this coach — a Mentora entry (CoachId IS NULL) or
        // another coach's row falls out of this predicate and 404s, never 403. Ownership wins
        // over body validation: an invalid body on someone else's exercise must still 404.
        var exercise = await WithDetail(db.Exercises)
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId && e.ExerciseCoachId == coachId, ct)
            ?? throw new NotFoundException($"Exercise {exerciseId} not found.");

        await validator.ValidateAndThrowAsync(request, ct);

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

    public async Task<ExerciseListResponseDto> ListForMemberAsync(
        Guid memberId, Guid? muscleGroupId, Guid? equipmentId, Guid? objectiveId, string? level,
        string? search, int limit, string? cursor, CancellationToken ct)
    {
        if (limit is < 1 or > 100)
            throw new InvalidOperationException($"limit must be between 1 and 100 (got {limit}).");

        var maxLevel = ParseLevelFilter(level);

        string? term = null;
        if (search is not null)
        {
            term = search.Trim();
            if (term.Length is < SearchMinLength or > SearchMaxLength)
                throw new InvalidOperationException(
                    $"search must be between {SearchMinLength} and {SearchMaxLength} characters (got {term.Length}).");
        }

        var query = ApplyCatalogFilters(VisibleToMember(memberId), muscleGroupId, equipmentId, objectiveId, maxLevel);

        // Single query: fetch limit+1 to know whether a next page exists.
        List<ListRow> rows;
        if (term is null)
        {
            var cursorPayload = cursor is null ? null : DecodeCursor(cursor);
            if (cursorPayload is not null)
            {
                var cursorName = cursorPayload.Name;
                var cursorId   = cursorPayload.ExerciseId;
                query = query.Where(e => e.ExerciseName.CompareTo(cursorName) > 0
                                      || (e.ExerciseName == cursorName && e.ExerciseId.CompareTo(cursorId) > 0));
            }

            rows = await query
                .AsNoTracking()
                .OrderBy(e => e.ExerciseName)
                .ThenBy(e => e.ExerciseId)
                .Take(limit + 1)
                .Select(e => new ListRow(
                    e.ExerciseId,
                    e.ExerciseName,
                    e.ExerciseMinLevel,
                    e.ExercisePrimaryEquipmentId,
                    e.PrimaryEquipment != null ? e.PrimaryEquipment.EquipmentName : null,
                    e.ExerciseSecondaryEquipmentId,
                    e.SecondaryEquipment != null ? e.SecondaryEquipment.EquipmentName : null,
                    0))
                .ToListAsync(ct);
        }
        else
        {
            // Search: name and alternative names only, ranked by relevance (see ExerciseSearch),
            // then by name, then by id. The cursor carries the rank as well.
            var ranked = ExerciseSearch.WithRank(query.AsNoTracking(), term)
                .Where(r => r.Rank <= ExerciseSearch.MaxRank);

            var cursorPayload = cursor is null ? null : DecodeSearchCursor(cursor);
            if (cursorPayload is not null)
            {
                var cursorRank = cursorPayload.Rank!.Value;
                var cursorName = cursorPayload.Name;
                var cursorId   = cursorPayload.ExerciseId;
                ranked = ranked.Where(r => r.Rank > cursorRank
                                        || (r.Rank == cursorRank
                                            && (r.Exercise.ExerciseName.CompareTo(cursorName) > 0
                                                || (r.Exercise.ExerciseName == cursorName
                                                    && r.Exercise.ExerciseId.CompareTo(cursorId) > 0))));
            }

            rows = await ranked
                .OrderBy(r => r.Rank)
                .ThenBy(r => r.Exercise.ExerciseName)
                .ThenBy(r => r.Exercise.ExerciseId)
                .Take(limit + 1)
                .Select(r => new ListRow(
                    r.Exercise.ExerciseId,
                    r.Exercise.ExerciseName,
                    r.Exercise.ExerciseMinLevel,
                    r.Exercise.ExercisePrimaryEquipmentId,
                    r.Exercise.PrimaryEquipment != null ? r.Exercise.PrimaryEquipment.EquipmentName : null,
                    r.Exercise.ExerciseSecondaryEquipmentId,
                    r.Exercise.SecondaryEquipment != null ? r.Exercise.SecondaryEquipment.EquipmentName : null,
                    r.Rank))
                .ToListAsync(ct);
        }

        var hasNext  = rows.Count > limit;
        var pageRows = hasNext ? rows.Take(limit).ToList() : rows;

        var items = pageRows
            .Select(r => new ExerciseSummaryDto(
                r.ExerciseId,
                r.ExerciseName,
                r.ExerciseMinLevel is { } l ? EnumMappings.ExerciseLevelMapping.ToWire(l) : null,
                r.PrimaryId is { } pid && r.PrimaryName is not null ? new ExerciseEquipmentRef(pid, r.PrimaryName) : null,
                r.SecondaryId is { } sid && r.SecondaryName is not null ? new ExerciseEquipmentRef(sid, r.SecondaryName) : null))
            .ToList();

        string? nextCursor = null;
        if (hasNext)
        {
            var last = pageRows[^1];
            nextCursor = term is null
                ? EncodeCursor(last.ExerciseName, last.ExerciseId)
                : EncodeSearchCursor(last.Rank, last.ExerciseName, last.ExerciseId);
        }

        return new ExerciseListResponseDto(items, nextCursor);
    }

    public async Task<ExerciseResponse> GetByIdForMemberAsync(Guid exerciseId, Guid memberId, CancellationToken ct)
    {
        var exercise = await WithDetail(VisibleToMember(memberId).AsNoTracking())
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId, ct)
            ?? throw new NotFoundException($"Exercise {exerciseId} not found.");

        return ToResponse(exercise);
    }

    public Task<ExerciseFiltersResponse> GetFiltersForMemberAsync(Guid memberId, CancellationToken ct) =>
        BuildFiltersAsync(VisibleToMember(memberId), ct);

    public Task<ExerciseFiltersResponse> GetFiltersForCoachAsync(Guid coachId, CancellationToken ct) =>
        BuildFiltersAsync(
            db.Exercises.Where(e => e.ExerciseIsActive
                                 && (e.ExerciseCoachId == null || e.ExerciseCoachId == coachId)),
            ct);

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

    /// <summary>Active exercises a member can see: Mentora entries plus those of the coaches they are attached to.</summary>
    private IQueryable<Exercise> VisibleToMember(Guid memberId) =>
        db.Exercises.Where(e => e.ExerciseIsActive
                             && (e.ExerciseCoachId == null
                                 || db.MemberCoaches.Any(mc => mc.MemberId == memberId
                                                            && mc.CoachId == e.ExerciseCoachId)));

    private static IQueryable<Exercise> WithDetail(IQueryable<Exercise> query) =>
        query
            .Include(e => e.PrimaryMuscleGroup)
            .Include(e => e.PrimaryEquipment)
            .Include(e => e.SecondaryEquipment)
            .Include(e => e.Video)
            .Include(e => e.ExerciseObjectives).ThenInclude(o => o.Objective);

    private static IQueryable<Exercise> ApplyCatalogFilters(
        IQueryable<Exercise> query, Guid? muscleGroupId, Guid? equipmentId, Guid? objectiveId,
        ExerciseLevel? maxLevel)
    {
        if (muscleGroupId is { } mg)
            query = query.Where(e => e.ExercisePrimaryMuscleGroupId == mg);

        if (equipmentId is { } eq)
            query = query.Where(e => e.ExercisePrimaryEquipmentId == eq || e.ExerciseSecondaryEquipmentId == eq);

        if (objectiveId is { } obj)
            query = query.Where(e => e.ExerciseObjectives.Any(o => o.ExerciseObjectiveObjectiveId == obj));

        // Literal arrays inside the expression are inlined by EF Core as a constant IN ('BEGINNER', ...)
        // list, so the level ordering (BEGINNER < INTERMEDIATE < ADVANCED) is resolved in SQL.
        if (maxLevel is { } max)
            query = max switch
            {
                ExerciseLevel.Beginner => query.Where(e => e.ExerciseMinLevel != null
                    && new[] { ExerciseLevel.Beginner }.Contains(e.ExerciseMinLevel.Value)),
                ExerciseLevel.Intermediate => query.Where(e => e.ExerciseMinLevel != null
                    && new[] { ExerciseLevel.Beginner, ExerciseLevel.Intermediate }.Contains(e.ExerciseMinLevel.Value)),
                _ => query.Where(e => e.ExerciseMinLevel != null
                    && new[] { ExerciseLevel.Beginner, ExerciseLevel.Intermediate, ExerciseLevel.Advanced }.Contains(e.ExerciseMinLevel.Value)),
            };

        return query;
    }

    private async Task<ExerciseFiltersResponse> BuildFiltersAsync(IQueryable<Exercise> visible, CancellationToken ct)
    {
        var muscleGroups = await db.MuscleGroups
            .Where(g => visible.Any(e => e.ExercisePrimaryMuscleGroupId == g.MuscleGroupId))
            .OrderBy(g => g.MuscleGroupSortOrder).ThenBy(g => g.MuscleGroupName)
            .Select(g => new ExerciseFilterMuscleGroup(g.MuscleGroupId, g.MuscleGroupName, g.MuscleGroupSortOrder))
            .ToListAsync(ct);

        var equipment = await db.EquipmentItems
            .Where(q => visible.Any(e => e.ExercisePrimaryEquipmentId == q.EquipmentId
                                      || e.ExerciseSecondaryEquipmentId == q.EquipmentId))
            .OrderBy(q => q.EquipmentName == EquipmentItem.NoEquipmentName ? 0 : 1)
            .ThenBy(q => q.EquipmentName)
            .Select(q => new ExerciseFilterEquipment(q.EquipmentId, q.EquipmentName, q.EquipmentCategory))
            .ToListAsync(ct);

        var objectives = await db.Objectives
            .Where(o => visible.Any(e => e.ExerciseObjectives.Any(l => l.ExerciseObjectiveObjectiveId == o.ObjectiveId)))
            .OrderBy(o => o.ObjectiveName)
            .Select(o => new ExerciseObjectiveRef(o.ObjectiveId, o.ObjectiveName))
            .ToListAsync(ct);

        return new ExerciseFiltersResponse(muscleGroups, equipment, objectives, EnumMappings.ExerciseLevelMapping.WireValues);
    }

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

    /// <summary>Parses the <c>level</c> filter: the requested level is the highest one the result may contain.</summary>
    private static ExerciseLevel? ParseLevelFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim().ToUpperInvariant();
        if (!EnumMappings.ExerciseLevelMapping.WireValues.Contains(normalized))
            throw new InvalidOperationException(
                $"Invalid level '{value}'. Accepted values: {string.Join(", ", EnumMappings.ExerciseLevelMapping.WireValues)}.");

        return EnumMappings.ExerciseLevelMapping.Parse(normalized);
    }

    /// <summary>Escapes LIKE wildcards; paired with the backslash escape character in the ILIKE call.</summary>
    private static string EscapeLike(string value) => ExerciseSearch.EscapeLike(value);

    // ── Cursor helpers ─────────────────────────────────────────────────────────

    private static string EncodeCursor(string name, Guid exerciseId)
    {
        var payload = JsonSerializer.Serialize(new CursorPayload(name, exerciseId));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    private static CursorPayload DecodeCursor(string cursor)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var payload = JsonSerializer.Deserialize<CursorPayload>(json);
            if (payload is null || payload.Name is null)
                throw new InvalidOperationException("Invalid cursor.");
            return payload;
        }
        catch (FormatException)   { throw new InvalidOperationException("Invalid cursor format."); }
        catch (JsonException)     { throw new InvalidOperationException("Invalid cursor format."); }
        catch (ArgumentException) { throw new InvalidOperationException("Invalid cursor format."); }
    }

    private sealed record CursorPayload(string Name, Guid ExerciseId);

    // Search cursor: the position in the (rank, name, id) order. A cursor of the plain (name, id)
    // format has no rank and is rejected like any cursor of the wrong shape.
    private static string EncodeSearchCursor(int rank, string name, Guid exerciseId)
    {
        var payload = JsonSerializer.Serialize(new SearchCursorPayload(rank, name, exerciseId));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    private static SearchCursorPayload DecodeSearchCursor(string cursor)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var payload = JsonSerializer.Deserialize<SearchCursorPayload>(json);
            if (payload is null || payload.Name is null || payload.Rank is not (>= 0 and <= ExerciseSearch.MaxRank))
                throw new InvalidOperationException("Invalid cursor.");
            return payload;
        }
        catch (FormatException)   { throw new InvalidOperationException("Invalid cursor format."); }
        catch (JsonException)     { throw new InvalidOperationException("Invalid cursor format."); }
        catch (ArgumentException) { throw new InvalidOperationException("Invalid cursor format."); }
    }

    private sealed record SearchCursorPayload(int? Rank, string Name, Guid ExerciseId);

    private sealed record ListRow(
        Guid ExerciseId, string ExerciseName, ExerciseLevel? ExerciseMinLevel,
        Guid? PrimaryId, string? PrimaryName, Guid? SecondaryId, string? SecondaryName, int Rank);

    // ── Mapping ────────────────────────────────────────────────────────────────

    private static ExerciseEquipmentRef? ToEquipmentRef(EquipmentItem? e) =>
        e is null ? null : new ExerciseEquipmentRef(e.EquipmentId, e.EquipmentName);

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
        e.ExerciseUpdatedDate,
        e.ExerciseSourceCode,
        e.ExerciseNameEn,
        e.ExerciseCategory,
        e.ExerciseMovementPattern,
        e.ExerciseType,
        e.ExerciseLaterality,
        e.ExerciseStartingPosition,
        e.ExerciseGripType,
        e.PrimaryMuscleGroup is null
            ? null
            : new ExerciseMuscleGroupRef(e.PrimaryMuscleGroup.MuscleGroupId, e.PrimaryMuscleGroup.MuscleGroupName),
        e.ExerciseSecondaryMuscles ?? [],
        ToEquipmentRef(e.PrimaryEquipment),
        ToEquipmentRef(e.SecondaryEquipment),
        e.ExerciseMinLevel is { } level ? EnumMappings.ExerciseLevelMapping.ToWire(level) : null,
        e.ExerciseBreathing,
        e.ExerciseCommonMistakes ?? [],
        e.ExercisePrecautions,
        e.ExerciseCoachTip,
        e.ExerciseObjectives
            .Select(o => new ExerciseObjectiveRef(o.ExerciseObjectiveObjectiveId, o.Objective.ObjectiveName))
            .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToList(),
        e.Video is null
            ? null
            : new ExerciseVideoResponse(
                e.Video.ExerciseVideoMuxPlaybackId,
                EnumMappings.ExerciseVideoStatusMapping.ToWire(e.Video.ExerciseVideoStatus),
                e.Video.ExerciseVideoDurationSeconds,
                e.Video.ExerciseVideoAspectRatio)
    );
}
