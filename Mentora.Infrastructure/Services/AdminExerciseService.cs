using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using Mentora.Core.DTOs.AdminExercise;
using Mentora.Core.DTOs.Exercise;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Mentora.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static Mentora.Infrastructure.Services.ExerciseListPaging;

namespace Mentora.Infrastructure.Services;

/// <summary>
/// Lot 12.2b — admin back-office edition of the Mentora exercise catalog (EXERCISE_COACH_ID NULL only,
/// inactive included). The database is the source of truth: every write goes through the closed reference
/// lists, keeps the text mirrors in step, and leaves one ADMIN_AUDIT_LOG row in the same SaveChanges.
/// </summary>
public class AdminExerciseService(
    MentoraDbContext db,
    IValidator<AdminExerciseRequest> validator,
    IValidator<AdminExerciseActivationRequest> activationValidator) : IAdminExerciseService
{
    /// <summary>Fixed key of the advisory lock that serialises the source code allocation.</summary>
    private const string SourceCodeLockSql = "SELECT pg_advisory_xact_lock(7122002)";

    private const int MaxCategoryMirror = 60;
    private const int MaxTypeMirror = 60;
    private const int MaxLateralityMirror = 40;
    private const int MaxMovementPatternMirror = 60;
    private const int MaxStartingPositionMirror = 80;
    private const int MaxGripTypeMirror = 60;

    private const string PolyarticularTypeCode = "POLYARTICULAIRE";

    private static readonly Regex SourceCodeTerm =
        new("^(EX|LG)[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>At least one of the six lists holds text while its foreign key is null.</summary>
    private static readonly Expression<Func<Exercise, bool>> HasLegacy = e =>
        (e.ExerciseCategory != null && e.ExerciseCategoryId == null)
        || (e.ExerciseType != null && e.ExerciseTypeId == null)
        || (e.ExerciseLaterality != null && e.ExerciseLateralityId == null)
        || (e.ExerciseMovementPattern != null && e.ExerciseMovementPatternId == null)
        || (e.ExerciseStartingPosition != null && e.ExerciseStartingPositionId == null)
        || (e.ExerciseGripType != null && e.ExerciseGripTypeId == null);

    private static readonly Expression<Func<Exercise, bool>> HasNoLegacy = e =>
        !((e.ExerciseCategory != null && e.ExerciseCategoryId == null)
        || (e.ExerciseType != null && e.ExerciseTypeId == null)
        || (e.ExerciseLaterality != null && e.ExerciseLateralityId == null)
        || (e.ExerciseMovementPattern != null && e.ExerciseMovementPatternId == null)
        || (e.ExerciseStartingPosition != null && e.ExerciseStartingPositionId == null)
        || (e.ExerciseGripType != null && e.ExerciseGripTypeId == null));

    // ── References ─────────────────────────────────────────────────────────────

    public async Task<AdminExerciseReferencesResponse> GetReferencesAsync(CancellationToken ct)
    {
        var categories = await db.ExerciseCategories.AsNoTracking()
            .OrderBy(x => x.ExerciseCategorySortOrder).ThenBy(x => x.ExerciseCategoryLabel)
            .Select(x => new AdminExerciseReferenceItem(x.ExerciseCategoryId, x.ExerciseCategoryCode, x.ExerciseCategoryLabel, x.ExerciseCategorySortOrder, x.ExerciseCategoryIsActive))
            .ToListAsync(ct);
        var types = await db.ExerciseTypes.AsNoTracking()
            .OrderBy(x => x.ExerciseTypeSortOrder).ThenBy(x => x.ExerciseTypeLabel)
            .Select(x => new AdminExerciseReferenceItem(x.ExerciseTypeId, x.ExerciseTypeCode, x.ExerciseTypeLabel, x.ExerciseTypeSortOrder, x.ExerciseTypeIsActive))
            .ToListAsync(ct);
        var lateralities = await db.ExerciseLateralities.AsNoTracking()
            .OrderBy(x => x.ExerciseLateralitySortOrder).ThenBy(x => x.ExerciseLateralityLabel)
            .Select(x => new AdminExerciseReferenceItem(x.ExerciseLateralityId, x.ExerciseLateralityCode, x.ExerciseLateralityLabel, x.ExerciseLateralitySortOrder, x.ExerciseLateralityIsActive))
            .ToListAsync(ct);
        var patterns = await db.MovementPatterns.AsNoTracking()
            .OrderBy(x => x.MovementPatternSortOrder).ThenBy(x => x.MovementPatternLabel)
            .Select(x => new AdminExerciseReferenceItem(x.MovementPatternId, x.MovementPatternCode, x.MovementPatternLabel, x.MovementPatternSortOrder, x.MovementPatternIsActive))
            .ToListAsync(ct);
        var positions = await db.StartingPositions.AsNoTracking()
            .OrderBy(x => x.StartingPositionSortOrder).ThenBy(x => x.StartingPositionLabel)
            .Select(x => new AdminExerciseReferenceItem(x.StartingPositionId, x.StartingPositionCode, x.StartingPositionLabel, x.StartingPositionSortOrder, x.StartingPositionIsActive))
            .ToListAsync(ct);
        var grips = await db.GripTypes.AsNoTracking()
            .OrderBy(x => x.GripTypeSortOrder).ThenBy(x => x.GripTypeLabel)
            .Select(x => new AdminExerciseReferenceItem(x.GripTypeId, x.GripTypeCode, x.GripTypeLabel, x.GripTypeSortOrder, x.GripTypeIsActive))
            .ToListAsync(ct);

        var muscleGroups = await db.MuscleGroups.AsNoTracking()
            .OrderBy(g => g.MuscleGroupSortOrder).ThenBy(g => g.MuscleGroupName)
            .Select(g => new AdminMuscleGroupRef(g.MuscleGroupId, g.MuscleGroupName, g.MuscleGroupSortOrder))
            .ToListAsync(ct);
        var equipment = await db.EquipmentItems.AsNoTracking()
            .OrderBy(q => q.EquipmentName == EquipmentItem.NoEquipmentName ? 0 : 1)
            .ThenBy(q => q.EquipmentName)
            .Select(q => new AdminEquipmentRef(q.EquipmentId, q.EquipmentName, q.EquipmentCategory))
            .ToListAsync(ct);
        var objectives = await db.Objectives.AsNoTracking()
            .OrderBy(o => o.ObjectiveName)
            .Select(o => new AdminNamedRef(o.ObjectiveId, o.ObjectiveName))
            .ToListAsync(ct);

        return new AdminExerciseReferencesResponse(
            categories, types, lateralities, patterns, positions, grips,
            muscleGroups, equipment, objectives, EnumMappings.ExerciseLevelMapping.WireValues);
    }

    // ── List ───────────────────────────────────────────────────────────────────

    public async Task<AdminExerciseListResponse> ListAsync(
        string? search, bool? isActive, Guid? categoryId, Guid? muscleGroupId, Guid? equipmentId,
        Guid? objectiveId, string? level, bool? hasLegacyValues, int limit, string? cursor, CancellationToken ct)
    {
        ValidateLimit(limit);
        var exactLevel = ParseExactLevel(level);
        var term = NormalizeSearch(search);

        IQueryable<Exercise> query = db.Exercises.Where(e => e.ExerciseCoachId == null);

        if (isActive is { } active)
            query = query.Where(e => e.ExerciseIsActive == active);
        if (categoryId is { } cat)
            query = query.Where(e => e.ExerciseCategoryId == cat);
        if (muscleGroupId is { } mg)
            query = query.Where(e => e.ExercisePrimaryMuscleGroupId == mg);
        if (equipmentId is { } eq)
            query = query.Where(e => e.ExercisePrimaryEquipmentId == eq || e.ExerciseSecondaryEquipmentId == eq);
        if (objectiveId is { } obj)
            query = query.Where(e => e.ExerciseObjectives.Any(o => o.ExerciseObjectiveObjectiveId == obj));
        if (exactLevel is { } lv)
            query = query.Where(e => e.ExerciseMinLevel == lv);
        if (hasLegacyValues == true)
            query = query.Where(HasLegacy);
        else if (hasLegacyValues == false)
            query = query.Where(HasNoLegacy);

        // A term shaped like a source code (EX029, lg28) filters on that exact code; any other term is
        // the member search (name and alternative names, ranked).
        if (term is not null && SourceCodeTerm.IsMatch(term))
        {
            var code = term.ToUpperInvariant();
            query = query.Where(e => e.ExerciseSourceCode == code);
            term = null;
        }

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
                    e.ExerciseSourceCode,
                    e.ExerciseName,
                    e.CategoryItem != null ? e.CategoryItem.ExerciseCategoryLabel : null,
                    e.PrimaryMuscleGroup != null ? e.PrimaryMuscleGroup.MuscleGroupName : null,
                    e.PrimaryEquipment != null ? e.PrimaryEquipment.EquipmentName : null,
                    e.ExerciseMinLevel,
                    e.ExerciseIsActive,
                    e.Video != null,
                    (e.ExerciseCategory != null && e.ExerciseCategoryId == null)
                        || (e.ExerciseType != null && e.ExerciseTypeId == null)
                        || (e.ExerciseLaterality != null && e.ExerciseLateralityId == null)
                        || (e.ExerciseMovementPattern != null && e.ExerciseMovementPatternId == null)
                        || (e.ExerciseStartingPosition != null && e.ExerciseStartingPositionId == null)
                        || (e.ExerciseGripType != null && e.ExerciseGripTypeId == null),
                    e.ExerciseUpdatedDate,
                    0))
                .ToListAsync(ct);
        }
        else
        {
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
                    r.Exercise.ExerciseSourceCode,
                    r.Exercise.ExerciseName,
                    r.Exercise.CategoryItem != null ? r.Exercise.CategoryItem.ExerciseCategoryLabel : null,
                    r.Exercise.PrimaryMuscleGroup != null ? r.Exercise.PrimaryMuscleGroup.MuscleGroupName : null,
                    r.Exercise.PrimaryEquipment != null ? r.Exercise.PrimaryEquipment.EquipmentName : null,
                    r.Exercise.ExerciseMinLevel,
                    r.Exercise.ExerciseIsActive,
                    r.Exercise.Video != null,
                    (r.Exercise.ExerciseCategory != null && r.Exercise.ExerciseCategoryId == null)
                        || (r.Exercise.ExerciseType != null && r.Exercise.ExerciseTypeId == null)
                        || (r.Exercise.ExerciseLaterality != null && r.Exercise.ExerciseLateralityId == null)
                        || (r.Exercise.ExerciseMovementPattern != null && r.Exercise.ExerciseMovementPatternId == null)
                        || (r.Exercise.ExerciseStartingPosition != null && r.Exercise.ExerciseStartingPositionId == null)
                        || (r.Exercise.ExerciseGripType != null && r.Exercise.ExerciseGripTypeId == null),
                    r.Exercise.ExerciseUpdatedDate,
                    r.Rank))
                .ToListAsync(ct);
        }

        var hasNext  = rows.Count > limit;
        var pageRows = hasNext ? rows.Take(limit).ToList() : rows;

        var items = pageRows
            .Select(r => new AdminExerciseListItem(
                r.ExerciseId, r.SourceCode, r.Name, r.Category, r.PrimaryMuscleGroup, r.PrimaryEquipment,
                r.Level is { } l ? EnumMappings.ExerciseLevelMapping.ToWire(l) : null,
                r.IsActive, r.HasVideo, r.HasLegacy, r.UpdatedDate))
            .ToList();

        string? nextCursor = null;
        if (hasNext)
        {
            var last = pageRows[^1];
            nextCursor = term is null
                ? EncodeCursor(last.Name, last.ExerciseId)
                : EncodeSearchCursor(last.Rank, last.Name, last.ExerciseId);
        }

        return new AdminExerciseListResponse(items, nextCursor);
    }

    private sealed record ListRow(
        Guid ExerciseId, string? SourceCode, string Name, string? Category, string? PrimaryMuscleGroup,
        string? PrimaryEquipment, ExerciseLevel? Level, bool IsActive, bool HasVideo, bool HasLegacy,
        DateTime UpdatedDate, int Rank);

    private static ExerciseLevel? ParseExactLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim().ToUpperInvariant();
        if (!EnumMappings.ExerciseLevelMapping.WireValues.Contains(normalized))
            throw new InvalidOperationException(
                $"Invalid level '{value}'. Accepted values: {string.Join(", ", EnumMappings.ExerciseLevelMapping.WireValues)}.");

        return EnumMappings.ExerciseLevelMapping.Parse(normalized);
    }

    // ── Detail ─────────────────────────────────────────────────────────────────

    public Task<AdminExerciseResponse> GetByIdAsync(Guid exerciseId, CancellationToken ct) =>
        LoadDetailAsync(exerciseId, ct);

    private async Task<AdminExerciseResponse> LoadDetailAsync(Guid exerciseId, CancellationToken ct)
    {
        var exercise = await db.Exercises
            .AsNoTracking()
            .Include(e => e.CategoryItem)
            .Include(e => e.TypeItem)
            .Include(e => e.LateralityItem)
            .Include(e => e.MovementPatternItem)
            .Include(e => e.StartingPositionItem)
            .Include(e => e.GripTypeItem)
            .Include(e => e.PrimaryMuscleGroup)
            .Include(e => e.SecondaryMuscleGroups).ThenInclude(s => s.MuscleGroup)
            .Include(e => e.PrimaryEquipment)
            .Include(e => e.SecondaryEquipment)
            .Include(e => e.Video)
            .Include(e => e.ExerciseObjectives).ThenInclude(o => o.Objective)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId && e.ExerciseCoachId == null, ct)
            ?? throw new NotFoundException($"Exercise {exerciseId} not found.");

        var usage = await GetUsageAsync(exerciseId, ct);
        return ToDetail(exercise, usage);
    }

    private static AdminReferenceRef? Ref(Guid? id, string? code, string? label) =>
        id is { } i && code is not null && label is not null ? new AdminReferenceRef(i, code, label) : null;

    private static AdminNamedRef? Named(Guid? id, string? name) =>
        id is { } i && name is not null ? new AdminNamedRef(i, name) : null;

    private static string? LegacyText(Guid? foreignKey, string? text) =>
        foreignKey is null && text is not null ? text : null;

    private static AdminExerciseResponse ToDetail(Exercise e, AdminExerciseUsage usage) => new(
        e.ExerciseId,
        e.ExerciseSourceCode,
        e.ExerciseName,
        e.ExerciseNameEn,
        e.ExerciseDescription,
        e.ExerciseInstructions,
        Ref(e.ExerciseCategoryId, e.CategoryItem?.ExerciseCategoryCode, e.CategoryItem?.ExerciseCategoryLabel),
        Ref(e.ExerciseTypeId, e.TypeItem?.ExerciseTypeCode, e.TypeItem?.ExerciseTypeLabel),
        Ref(e.ExerciseLateralityId, e.LateralityItem?.ExerciseLateralityCode, e.LateralityItem?.ExerciseLateralityLabel),
        Ref(e.ExerciseMovementPatternId, e.MovementPatternItem?.MovementPatternCode, e.MovementPatternItem?.MovementPatternLabel),
        Ref(e.ExerciseStartingPositionId, e.StartingPositionItem?.StartingPositionCode, e.StartingPositionItem?.StartingPositionLabel),
        Ref(e.ExerciseGripTypeId, e.GripTypeItem?.GripTypeCode, e.GripTypeItem?.GripTypeLabel),
        new AdminExerciseLegacyValues(
            LegacyText(e.ExerciseCategoryId, e.ExerciseCategory),
            LegacyText(e.ExerciseTypeId, e.ExerciseType),
            LegacyText(e.ExerciseLateralityId, e.ExerciseLaterality),
            LegacyText(e.ExerciseMovementPatternId, e.ExerciseMovementPattern),
            LegacyText(e.ExerciseStartingPositionId, e.ExerciseStartingPosition),
            LegacyText(e.ExerciseGripTypeId, e.ExerciseGripType)),
        e.ExerciseIsPolyarticular,
        Named(e.ExercisePrimaryMuscleGroupId, e.PrimaryMuscleGroup?.MuscleGroupName),
        e.SecondaryMuscleGroups
            .Select(s => s.MuscleGroup)
            .OrderBy(g => g.MuscleGroupSortOrder).ThenBy(g => g.MuscleGroupName, StringComparer.Ordinal)
            .Select(g => new AdminNamedRef(g.MuscleGroupId, g.MuscleGroupName))
            .ToList(),
        Named(e.ExercisePrimaryEquipmentId, e.PrimaryEquipment?.EquipmentName),
        Named(e.ExerciseSecondaryEquipmentId, e.SecondaryEquipment?.EquipmentName),
        e.ExerciseMinLevel is { } level ? EnumMappings.ExerciseLevelMapping.ToWire(level) : null,
        e.ExerciseBreathing,
        e.ExerciseCommonMistakes ?? [],
        e.ExercisePrecautions,
        e.ExerciseCoachTip,
        e.ExerciseSearchTags,
        e.ExerciseObjectives
            .Select(o => new AdminNamedRef(o.ExerciseObjectiveObjectiveId, o.Objective.ObjectiveName))
            .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToList(),
        e.Video is null
            ? null
            : new ExerciseVideoResponse(
                e.Video.ExerciseVideoMuxPlaybackId,
                EnumMappings.ExerciseVideoStatusMapping.ToWire(e.Video.ExerciseVideoStatus),
                e.Video.ExerciseVideoDurationSeconds,
                e.Video.ExerciseVideoAspectRatio),
        e.ExerciseIsActive,
        usage,
        e.ExerciseCreatedDate,
        e.ExerciseUpdatedDate);

    // ── Usage (decision A: any status, any owner; jsonb searched at any depth on the key exerciseId) ──

    private async Task<AdminExerciseUsage> GetUsageAsync(Guid exerciseId, CancellationToken ct)
    {
        var idText = exerciseId.ToString();

        var programs = await db.ProgramExercises
            .Where(p => p.ProgramExerciseExerciseId == exerciseId)
            .Select(p => p.ProgramExerciseProgramId)
            .Distinct()
            .CountAsync(ct);

        var programTemplates = await db.Database
            .SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM "PROGRAM_TEMPLATES"
                WHERE jsonb_path_exists("PROGRAM_TEMPLATE_BODY", '$.**.exerciseId ? (@ == $id)', jsonb_build_object('id', {idText}))
                """)
            .SingleAsync(ct);

        var sessionTemplates = await db.Database
            .SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM "SESSION_TEMPLATES"
                WHERE jsonb_path_exists("SESSION_TEMPLATE_CONTENT", '$.**.exerciseId ? (@ == $id)', jsonb_build_object('id', {idText}))
                """)
            .SingleAsync(ct);

        return new AdminExerciseUsage(programs, programTemplates, sessionTemplates);
    }

    /// <summary>Names of the Mentora-owned templates using the exercise (at most 10 each), for the 409 details only.</summary>
    private async Task<(List<string> Programs, List<string> Sessions)> GetMentoraTemplateNamesAsync(
        Guid exerciseId, CancellationToken ct)
    {
        var idText = exerciseId.ToString();

        var programNames = await db.Database
            .SqlQuery<string>($"""
                SELECT "PROGRAM_TEMPLATE_NAME" AS "Value" FROM "PROGRAM_TEMPLATES"
                WHERE "PROGRAM_TEMPLATE_COACH_ID" IS NULL
                  AND jsonb_path_exists("PROGRAM_TEMPLATE_BODY", '$.**.exerciseId ? (@ == $id)', jsonb_build_object('id', {idText}))
                ORDER BY "PROGRAM_TEMPLATE_NAME" LIMIT 10
                """)
            .ToListAsync(ct);

        var sessionNames = await db.Database
            .SqlQuery<string>($"""
                SELECT "SESSION_TEMPLATE_NAME" AS "Value" FROM "SESSION_TEMPLATES"
                WHERE "COACH_ID" IS NULL
                  AND jsonb_path_exists("SESSION_TEMPLATE_CONTENT", '$.**.exerciseId ? (@ == $id)', jsonb_build_object('id', {idText}))
                ORDER BY "SESSION_TEMPLATE_NAME" LIMIT 10
                """)
            .ToListAsync(ct);

        return (programNames.OrderBy(n => n, StringComparer.Ordinal).ToList(),
                sessionNames.OrderBy(n => n, StringComparer.Ordinal).ToList());
    }

    // ── Create ─────────────────────────────────────────────────────────────────

    public async Task<AdminExerciseResponse> CreateAsync(
        AdminExerciseRequest request, Guid adminUserId, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureAdminAsync(adminUserId, ct);

        var refs = await ResolveReferencesAsync(request, null, ct);
        var state = BuildState(request, refs, null);
        var now = DateTime.UtcNow;

        Guid exerciseId;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Two simultaneous creations take the lock one after the other, so they read two different maxima.
            await db.Database.ExecuteSqlRawAsync(SourceCodeLockSql, ct);

            await EnsureNameFreeAsync(state.Name, Guid.Empty, ct);

            var highest = await db.Database
                .SqlQuery<int>($"""
                    SELECT COALESCE(MAX(substring("EXERCISE_SOURCE_CODE" from 3)::int), 0)::int AS "Value"
                    FROM "EXERCISES" WHERE "EXERCISE_SOURCE_CODE" ~ '^EX[0-9]+$'
                    """)
                .SingleAsync(ct);

            var exercise = new Exercise
            {
                ExerciseId           = Guid.NewGuid(),
                ExerciseCoachId      = null,
                ExerciseSourceCode   = "EX" + (highest + 1).ToString("D3"),
                ExerciseIsActive     = true,
                ExerciseCreatedDate  = now,
                ExerciseUpdatedDate  = now,
            };
            exerciseId = exercise.ExerciseId;

            ApplyState(exercise, state);
            foreach (var groupId in state.SecondaryGroupIds)
                exercise.SecondaryMuscleGroups.Add(new ExerciseSecondaryMuscleGroup
                {
                    ExerciseSecondaryMuscleGroupExerciseId    = exercise.ExerciseId,
                    ExerciseSecondaryMuscleGroupMuscleGroupId = groupId,
                });
            foreach (var objectiveId in state.ObjectiveIds)
                exercise.ExerciseObjectives.Add(new ExerciseObjective
                {
                    ExerciseObjectiveExerciseId  = exercise.ExerciseId,
                    ExerciseObjectiveObjectiveId = objectiveId,
                });

            var changes = new Dictionary<string, object?> { ["sourceCode"] = exercise.ExerciseSourceCode, ["isActive"] = true };
            foreach (var (field, value) in RequestFields(state))
                changes[field] = value;

            db.Exercises.Add(exercise);
            db.AdminAuditLogs.Add(NewAudit(adminUserId, AdminAuditAction.ExerciseCreated, exercise.ExerciseId, changes, now));

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsNameViolation(ex))
        {
            db.ChangeTracker.Clear();
            await EnsureNameFreeAsync(state.Name, Guid.Empty, ct);
            throw new ConflictException("EXERCISE_NAME_TAKEN");
        }

        return await LoadDetailAsync(exerciseId, ct);
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    public async Task<AdminExerciseResponse> UpdateAsync(
        Guid exerciseId, AdminExerciseRequest request, Guid adminUserId, CancellationToken ct)
    {
        // Mentora rows only: a coach-owned exercise or an unknown id is a 404, and wins over body validation.
        var exercise = await db.Exercises
            .Include(e => e.SecondaryMuscleGroups)
            .Include(e => e.ExerciseObjectives)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId && e.ExerciseCoachId == null, ct)
            ?? throw new NotFoundException($"Exercise {exerciseId} not found.");

        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureAdminAsync(adminUserId, ct);

        var refs = await ResolveReferencesAsync(request, exercise, ct);
        await EnsureNameFreeAsync(Clean(request.Name)!, exercise.ExerciseId, ct);

        var before = StateOf(exercise);
        var after  = BuildState(request, refs, exercise);

        if (StateKey(before) == StateKey(after))
            return await LoadDetailAsync(exerciseId, ct);

        // Audit: only the request fields that changed. A change that touches mirrors or derived columns
        // only (a drifted mirror being corrected) is listed under their column names instead of nothing.
        var changes = DiffFields(RequestFields(before), RequestFields(after));
        if (changes.Count == 0)
            changes = DiffFields(MirrorFields(before), MirrorFields(after));

        var now = DateTime.UtcNow;
        ApplyState(exercise, after);
        exercise.ExerciseUpdatedDate = now;

        var existingGroups = exercise.SecondaryMuscleGroups.Select(l => l.ExerciseSecondaryMuscleGroupMuscleGroupId).ToHashSet();
        foreach (var link in exercise.SecondaryMuscleGroups
                     .Where(l => !after.SecondaryGroupIds.Contains(l.ExerciseSecondaryMuscleGroupMuscleGroupId)).ToList())
            db.ExerciseSecondaryMuscleGroups.Remove(link);
        foreach (var groupId in after.SecondaryGroupIds.Where(g => !existingGroups.Contains(g)))
            exercise.SecondaryMuscleGroups.Add(new ExerciseSecondaryMuscleGroup
            {
                ExerciseSecondaryMuscleGroupExerciseId    = exercise.ExerciseId,
                ExerciseSecondaryMuscleGroupMuscleGroupId = groupId,
            });

        var existingObjectives = exercise.ExerciseObjectives.Select(l => l.ExerciseObjectiveObjectiveId).ToHashSet();
        foreach (var link in exercise.ExerciseObjectives
                     .Where(l => !after.ObjectiveIds.Contains(l.ExerciseObjectiveObjectiveId)).ToList())
            db.ExerciseObjectives.Remove(link);
        foreach (var objectiveId in after.ObjectiveIds.Where(o => !existingObjectives.Contains(o)))
            exercise.ExerciseObjectives.Add(new ExerciseObjective
            {
                ExerciseObjectiveExerciseId  = exercise.ExerciseId,
                ExerciseObjectiveObjectiveId = objectiveId,
            });

        db.AdminAuditLogs.Add(NewAudit(adminUserId, AdminAuditAction.ExerciseUpdated, exercise.ExerciseId, changes, now));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsNameViolation(ex))
        {
            db.ChangeTracker.Clear();
            await EnsureNameFreeAsync(after.Name, exerciseId, ct);
            throw new ConflictException("EXERCISE_NAME_TAKEN");
        }

        return await LoadDetailAsync(exerciseId, ct);
    }

    // ── Activation ─────────────────────────────────────────────────────────────

    public async Task<AdminExerciseResponse> SetActivationAsync(
        Guid exerciseId, AdminExerciseActivationRequest request, Guid adminUserId, CancellationToken ct)
    {
        var exercise = await db.Exercises
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId && e.ExerciseCoachId == null, ct)
            ?? throw new NotFoundException($"Exercise {exerciseId} not found.");

        await activationValidator.ValidateAndThrowAsync(request, ct);
        await EnsureAdminAsync(adminUserId, ct);

        var target = request.IsActive!.Value;
        if (exercise.ExerciseIsActive == target)
            return await LoadDetailAsync(exerciseId, ct);

        if (!target)
        {
            var usage = await GetUsageAsync(exerciseId, ct);
            if (usage.Programs > 0 || usage.ProgramTemplates > 0 || usage.SessionTemplates > 0)
            {
                var (programNames, sessionNames) = await GetMentoraTemplateNamesAsync(exerciseId, ct);
                throw new ConflictException("EXERCISE_IN_USE", new
                {
                    programs                     = usage.Programs,
                    programTemplates             = usage.ProgramTemplates,
                    sessionTemplates             = usage.SessionTemplates,
                    mentoraProgramTemplateNames  = programNames,
                    mentoraSessionTemplateNames  = sessionNames,
                });
            }
        }

        var now = DateTime.UtcNow;
        exercise.ExerciseIsActive    = target;
        exercise.ExerciseUpdatedDate = now;

        var changes = new Dictionary<string, object?>
        {
            ["isActive"] = new Dictionary<string, object?> { ["before"] = !target, ["after"] = target },
        };
        db.AdminAuditLogs.Add(NewAudit(
            adminUserId,
            target ? AdminAuditAction.ExerciseActivated : AdminAuditAction.ExerciseDeactivated,
            exercise.ExerciseId, changes, now));

        await db.SaveChangesAsync(ct);
        return await LoadDetailAsync(exerciseId, ct);
    }

    // ── Admin and audit ────────────────────────────────────────────────────────

    /// <summary>The token of a disabled or deleted admin stays valid until it expires: refuse its writes.</summary>
    private async Task EnsureAdminAsync(Guid adminUserId, CancellationToken ct)
    {
        var ok = await db.AdminUsers.AnyAsync(a => a.AdminUserId == adminUserId && a.AdminUserIsEnabled, ct);
        if (!ok)
            throw new UnauthorizedException("Admin account not found or disabled.");
    }

    private static AdminAuditLog NewAudit(
        Guid adminUserId, AdminAuditAction action, Guid exerciseId, object changes, DateTime now) => new()
    {
        AdminAuditLogId          = Guid.NewGuid(),
        AdminAuditLogAdminUserId = adminUserId,
        AdminAuditLogAction      = action,
        AdminAuditLogEntityType  = AdminAuditEntityType.Exercise,
        AdminAuditLogEntityId    = exerciseId,
        AdminAuditLogChanges     = JsonSerializer.Serialize(changes),
        AdminAuditLogCreatedDate = now,
    };

    // ── Name uniqueness ────────────────────────────────────────────────────────

    /// <summary>409 EXERCISE_NAME_TAKEN when another Mentora exercise has the same name (accents, case, spaces ignored).</summary>
    private async Task EnsureNameFreeAsync(string name, Guid excludeExerciseId, CancellationToken ct)
    {
        var key = SeedingText.FoldedKey(name);
        var rows = await db.Exercises
            .AsNoTracking()
            .Where(e => e.ExerciseCoachId == null && e.ExerciseId != excludeExerciseId)
            .Select(e => new { e.ExerciseId, e.ExerciseSourceCode, e.ExerciseName })
            .ToListAsync(ct);

        var hit = rows.FirstOrDefault(r => SeedingText.FoldedKey(r.ExerciseName) == key);
        if (hit is not null)
            throw new ConflictException("EXERCISE_NAME_TAKEN", new
            {
                exerciseId = hit.ExerciseId,
                sourceCode = hit.ExerciseSourceCode,
                name       = hit.ExerciseName,
            });
    }

    private static bool IsNameViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg
        && pg.SqlState == PostgresErrorCodes.UniqueViolation
        && (pg.ConstraintName == "UX_EXERCISES_MENTORA_NAME_CI" || pg.ConstraintName == "IX_EXERCISES_COACH_NAME");

    // ── Reference resolution (existence rules) ─────────────────────────────────

    private sealed record Refs(
        ExerciseCategoryItem? Category, ExerciseTypeItem? Type, ExerciseLateralityItem? Laterality,
        MovementPatternItem? MovementPattern, StartingPositionItem? StartingPosition, GripTypeItem? GripType,
        MuscleGroupItem? PrimaryGroup, List<MuscleGroupItem> SecondaryGroups,
        EquipmentItem PrimaryEquipment, EquipmentItem? SecondaryEquipment, List<Objective> Objectives);

    /// <summary>
    /// Every id must exist; a reference list row must be active unless it is the value the exercise already
    /// holds. All problems are raised together as one FluentValidation ValidationException keyed by property name.
    /// </summary>
    private async Task<Refs> ResolveReferencesAsync(AdminExerciseRequest request, Exercise? current, CancellationToken ct)
    {
        var errors = new List<ValidationFailure>();

        async Task<T?> Lookup<T>(string property, Guid? id, Guid? heldId, Func<T, bool> isActive, Func<T, string> label, int maxMirror)
            where T : class
        {
            if (id is null) return null;
            var row = await db.Set<T>().FindAsync(new object[] { id.Value }, ct);
            if (row is null)
            {
                errors.Add(new ValidationFailure(property, $"'{property}' does not exist."));
                return null;
            }
            if (!isActive(row) && id != heldId)
                errors.Add(new ValidationFailure(property, $"'{property}' refers to an inactive value."));
            if (label(row).Length > maxMirror)
                errors.Add(new ValidationFailure(property, $"The label of '{property}' is too long for its column ({maxMirror} characters)."));
            return row;
        }

        var category = await Lookup<ExerciseCategoryItem>(nameof(request.CategoryId), request.CategoryId, current?.ExerciseCategoryId,
            x => x.ExerciseCategoryIsActive, x => x.ExerciseCategoryLabel, MaxCategoryMirror);
        var type = await Lookup<ExerciseTypeItem>(nameof(request.TypeId), request.TypeId, current?.ExerciseTypeId,
            x => x.ExerciseTypeIsActive, x => x.ExerciseTypeLabel, MaxTypeMirror);
        var laterality = await Lookup<ExerciseLateralityItem>(nameof(request.LateralityId), request.LateralityId, current?.ExerciseLateralityId,
            x => x.ExerciseLateralityIsActive, x => x.ExerciseLateralityLabel, MaxLateralityMirror);
        var pattern = await Lookup<MovementPatternItem>(nameof(request.MovementPatternId), request.MovementPatternId, current?.ExerciseMovementPatternId,
            x => x.MovementPatternIsActive, x => x.MovementPatternLabel, MaxMovementPatternMirror);
        var position = await Lookup<StartingPositionItem>(nameof(request.StartingPositionId), request.StartingPositionId, current?.ExerciseStartingPositionId,
            x => x.StartingPositionIsActive, x => x.StartingPositionLabel, MaxStartingPositionMirror);
        var grip = await Lookup<GripTypeItem>(nameof(request.GripTypeId), request.GripTypeId, current?.ExerciseGripTypeId,
            x => x.GripTypeIsActive, x => x.GripTypeLabel, MaxGripTypeMirror);

        // Muscle groups: the primary one and the secondary ones in one query.
        var secondaryIds = (request.SecondaryMuscleGroupIds ?? []).Distinct().ToList();
        var groupIds = secondaryIds.ToList();
        if (request.PrimaryMuscleGroupId is { } primaryGroupId)
            groupIds.Add(primaryGroupId);
        var groups = groupIds.Count == 0
            ? []
            : await db.MuscleGroups.Where(g => groupIds.Contains(g.MuscleGroupId)).ToListAsync(ct);

        MuscleGroupItem? primaryGroup = null;
        if (request.PrimaryMuscleGroupId is { } pg)
        {
            primaryGroup = groups.FirstOrDefault(g => g.MuscleGroupId == pg);
            if (primaryGroup is null)
                errors.Add(new ValidationFailure(nameof(request.PrimaryMuscleGroupId), "'PrimaryMuscleGroupId' does not exist."));
        }
        var secondaryGroups = new List<MuscleGroupItem>();
        foreach (var id in secondaryIds)
        {
            var group = groups.FirstOrDefault(g => g.MuscleGroupId == id);
            if (group is null)
                errors.Add(new ValidationFailure(nameof(request.SecondaryMuscleGroupIds), $"Muscle group {id} does not exist."));
            else
                secondaryGroups.Add(group);
        }

        // Equipment: primary (required by the validator) and secondary in one query.
        var equipmentIds = new List<Guid>();
        if (request.PrimaryEquipmentId is { } pe) equipmentIds.Add(pe);
        if (request.SecondaryEquipmentId is { } se) equipmentIds.Add(se);
        var equipment = await db.EquipmentItems.Where(q => equipmentIds.Contains(q.EquipmentId)).ToListAsync(ct);

        var primaryEquipment = equipment.FirstOrDefault(q => q.EquipmentId == request.PrimaryEquipmentId);
        if (primaryEquipment is null)
            errors.Add(new ValidationFailure(nameof(request.PrimaryEquipmentId), "'PrimaryEquipmentId' does not exist."));
        EquipmentItem? secondaryEquipment = null;
        if (request.SecondaryEquipmentId is { } sid)
        {
            secondaryEquipment = equipment.FirstOrDefault(q => q.EquipmentId == sid);
            if (secondaryEquipment is null)
                errors.Add(new ValidationFailure(nameof(request.SecondaryEquipmentId), "'SecondaryEquipmentId' does not exist."));
            else if (secondaryEquipment.EquipmentName == EquipmentItem.NoEquipmentName)
                errors.Add(new ValidationFailure(nameof(request.SecondaryEquipmentId), "'SecondaryEquipmentId' must not be \"Sans matériel\"."));
        }

        // Objectives.
        var objectiveIds = (request.ObjectiveIds ?? []).Distinct().ToList();
        var objectives = objectiveIds.Count == 0
            ? []
            : await db.Objectives.Where(o => objectiveIds.Contains(o.ObjectiveId)).ToListAsync(ct);
        foreach (var id in objectiveIds.Where(id => objectives.All(o => o.ObjectiveId != id)))
            errors.Add(new ValidationFailure(nameof(request.ObjectiveIds), $"Objective {id} does not exist."));

        if (errors.Count > 0)
            throw new ValidationException(errors);

        return new Refs(category, type, laterality, pattern, position, grip,
            primaryGroup, secondaryGroups, primaryEquipment!, secondaryEquipment, objectives);
    }

    // ── State: every stored value an admin write can change ────────────────────

    private sealed record State(
        string Name, string? NameEn, string? Description, string? Instructions,
        Guid? CategoryId, string? CategoryText, Guid? TypeId, string? TypeText,
        Guid? LateralityId, string? LateralityText, Guid? MovementPatternId, string? MovementPatternText,
        Guid? StartingPositionId, string? StartingPositionText, Guid? GripTypeId, string? GripTypeText,
        Guid? PrimaryMuscleGroupId, List<Guid> SecondaryGroupIds, List<string>? SecondaryMirror,
        Guid? PrimaryEquipmentId, Guid? SecondaryEquipmentId, ExerciseLevel? Level,
        string? Breathing, List<string>? CommonMistakes, string? Precautions, string? CoachTip,
        string? SearchTags, List<Guid> ObjectiveIds, bool IsPolyarticular,
        MuscleGroup LegacyMuscleGroup, Equipment LegacyEquipment);

    /// <summary>Trimmed text, null when empty.</summary>
    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Rule of the six lists: id set = foreign key set and mirror = the row's label. Id null with a foreign
    /// key already set clears both; id null with no foreign key leaves the text (legacy value kept).
    /// </summary>
    private static (Guid? Id, string? Text) Mirror(Guid? requestedId, string? label, Guid? currentFk, string? currentText)
    {
        if (requestedId is not null) return (requestedId, label);
        if (currentFk is not null)   return (null, null);
        return (null, currentText);
    }

    private static State BuildState(AdminExerciseRequest r, Refs refs, Exercise? current)
    {
        var category   = Mirror(r.CategoryId,         refs.Category?.ExerciseCategoryLabel,           current?.ExerciseCategoryId,         current?.ExerciseCategory);
        var type       = Mirror(r.TypeId,             refs.Type?.ExerciseTypeLabel,                   current?.ExerciseTypeId,             current?.ExerciseType);
        var laterality = Mirror(r.LateralityId,       refs.Laterality?.ExerciseLateralityLabel,       current?.ExerciseLateralityId,       current?.ExerciseLaterality);
        var pattern    = Mirror(r.MovementPatternId,  refs.MovementPattern?.MovementPatternLabel,     current?.ExerciseMovementPatternId,  current?.ExerciseMovementPattern);
        var position   = Mirror(r.StartingPositionId, refs.StartingPosition?.StartingPositionLabel,   current?.ExerciseStartingPositionId, current?.ExerciseStartingPosition);
        var grip       = Mirror(r.GripTypeId,         refs.GripType?.GripTypeLabel,                   current?.ExerciseGripTypeId,         current?.ExerciseGripType);

        var secondaryNames = refs.SecondaryGroups
            .OrderBy(g => g.MuscleGroupSortOrder).ThenBy(g => g.MuscleGroupName, StringComparer.Ordinal)
            .Select(g => g.MuscleGroupName)
            .ToList();

        var mistakes = (r.CommonMistakes ?? []).Select(m => m.Trim()).ToList();

        ExerciseLevel? level = Clean(r.Level) is { } wire
            ? EnumMappings.ExerciseLevelMapping.Parse(wire.ToUpperInvariant())
            : null;

        return new State(
            Clean(r.Name)!, Clean(r.NameEn), Clean(r.Description), Clean(r.Instructions),
            category.Id, category.Text, type.Id, type.Text,
            laterality.Id, laterality.Text, pattern.Id, pattern.Text,
            position.Id, position.Text, grip.Id, grip.Text,
            r.PrimaryMuscleGroupId,
            refs.SecondaryGroups.Select(g => g.MuscleGroupId).OrderBy(g => g).ToList(),
            secondaryNames.Count == 0 ? null : secondaryNames,
            r.PrimaryEquipmentId, r.SecondaryEquipmentId, level,
            Clean(r.Breathing), mistakes.Count == 0 ? null : mistakes, Clean(r.Precautions), Clean(r.CoachTip),
            Clean(r.SearchTags),
            refs.Objectives.Select(o => o.ObjectiveId).OrderBy(o => o).ToList(),
            refs.Type is not null && refs.Type.ExerciseTypeCode == PolyarticularTypeCode,
            LegacyExerciseColumns.MuscleGroupFor(category.Text, refs.PrimaryGroup, null),
            LegacyExerciseColumns.EquipmentFor(refs.PrimaryEquipment));
    }

    /// <summary>State currently stored (the exercise must have its two link collections loaded).</summary>
    private static State StateOf(Exercise e) => new(
        e.ExerciseName, e.ExerciseNameEn, e.ExerciseDescription, e.ExerciseInstructions,
        e.ExerciseCategoryId, e.ExerciseCategory, e.ExerciseTypeId, e.ExerciseType,
        e.ExerciseLateralityId, e.ExerciseLaterality, e.ExerciseMovementPatternId, e.ExerciseMovementPattern,
        e.ExerciseStartingPositionId, e.ExerciseStartingPosition, e.ExerciseGripTypeId, e.ExerciseGripType,
        e.ExercisePrimaryMuscleGroupId,
        e.SecondaryMuscleGroups.Select(l => l.ExerciseSecondaryMuscleGroupMuscleGroupId).OrderBy(g => g).ToList(),
        e.ExerciseSecondaryMuscles is { Count: > 0 } mirror ? mirror : null,
        e.ExercisePrimaryEquipmentId, e.ExerciseSecondaryEquipmentId, e.ExerciseMinLevel,
        e.ExerciseBreathing, e.ExerciseCommonMistakes is { Count: > 0 } mistakes ? mistakes : null,
        e.ExercisePrecautions, e.ExerciseCoachTip, e.ExerciseSearchTags,
        e.ExerciseObjectives.Select(l => l.ExerciseObjectiveObjectiveId).OrderBy(o => o).ToList(),
        e.ExerciseIsPolyarticular, e.ExerciseMuscleGroup, e.ExerciseEquipment);

    private static string StateKey(State s) => JsonSerializer.Serialize(s);

    /// <summary>Copies a state onto the entity columns (links are handled by the callers).</summary>
    private static void ApplyState(Exercise e, State s)
    {
        e.ExerciseName                 = s.Name;
        e.ExerciseNameEn               = s.NameEn;
        e.ExerciseDescription          = s.Description;
        e.ExerciseInstructions         = s.Instructions;
        e.ExerciseCategoryId           = s.CategoryId;
        e.ExerciseCategory             = s.CategoryText;
        e.ExerciseTypeId               = s.TypeId;
        e.ExerciseType                 = s.TypeText;
        e.ExerciseLateralityId         = s.LateralityId;
        e.ExerciseLaterality           = s.LateralityText;
        e.ExerciseMovementPatternId    = s.MovementPatternId;
        e.ExerciseMovementPattern      = s.MovementPatternText;
        e.ExerciseStartingPositionId   = s.StartingPositionId;
        e.ExerciseStartingPosition     = s.StartingPositionText;
        e.ExerciseGripTypeId           = s.GripTypeId;
        e.ExerciseGripType             = s.GripTypeText;
        e.ExercisePrimaryMuscleGroupId = s.PrimaryMuscleGroupId;
        e.ExerciseSecondaryMuscles     = s.SecondaryMirror;
        e.ExercisePrimaryEquipmentId   = s.PrimaryEquipmentId;
        e.ExerciseSecondaryEquipmentId = s.SecondaryEquipmentId;
        e.ExerciseMinLevel             = s.Level;
        e.ExerciseBreathing            = s.Breathing;
        e.ExerciseCommonMistakes       = s.CommonMistakes;
        e.ExercisePrecautions          = s.Precautions;
        e.ExerciseCoachTip             = s.CoachTip;
        e.ExerciseSearchTags           = s.SearchTags;
        e.ExerciseIsPolyarticular      = s.IsPolyarticular;
        e.ExerciseMuscleGroup          = s.LegacyMuscleGroup;
        e.ExerciseEquipment            = s.LegacyEquipment;

        // Only the foreign key columns are assigned. Never null a navigation here: a tracked principal
        // (loaded by ResolveReferencesAsync) makes EF read that as severing the relationship and write
        // the foreign key as NULL. The response reloads the exercise, so stale navigations are harmless.
    }

    // ── Audit field lists (request field names) ────────────────────────────────

    private static List<KeyValuePair<string, object?>> RequestFields(State s) =>
    [
        new("name", s.Name),
        new("nameEn", s.NameEn),
        new("description", s.Description),
        new("instructions", s.Instructions),
        new("categoryId", s.CategoryId),
        new("typeId", s.TypeId),
        new("lateralityId", s.LateralityId),
        new("movementPatternId", s.MovementPatternId),
        new("startingPositionId", s.StartingPositionId),
        new("gripTypeId", s.GripTypeId),
        new("primaryMuscleGroupId", s.PrimaryMuscleGroupId),
        new("secondaryMuscleGroupIds", s.SecondaryGroupIds),
        new("primaryEquipmentId", s.PrimaryEquipmentId),
        new("secondaryEquipmentId", s.SecondaryEquipmentId),
        new("level", s.Level is { } l ? EnumMappings.ExerciseLevelMapping.ToWire(l) : null),
        new("breathing", s.Breathing),
        new("commonMistakes", s.CommonMistakes),
        new("precautions", s.Precautions),
        new("coachTip", s.CoachTip),
        new("searchTags", s.SearchTags),
        new("objectiveIds", s.ObjectiveIds),
    ];

    private static List<KeyValuePair<string, object?>> MirrorFields(State s) =>
    [
        new("categoryText", s.CategoryText),
        new("typeText", s.TypeText),
        new("lateralityText", s.LateralityText),
        new("movementPatternText", s.MovementPatternText),
        new("startingPositionText", s.StartingPositionText),
        new("gripTypeText", s.GripTypeText),
        new("secondaryMuscles", s.SecondaryMirror),
        new("isPolyarticular", s.IsPolyarticular),
        new("legacyMuscleGroup", EnumMappings.MuscleGroupMapping.ToWire(s.LegacyMuscleGroup)),
        new("legacyEquipment", EnumMappings.EquipmentMapping.ToWire(s.LegacyEquipment)),
    ];

    private static List<object> DiffFields(
        List<KeyValuePair<string, object?>> before, List<KeyValuePair<string, object?>> after)
    {
        var diff = new List<object>();
        for (var i = 0; i < before.Count; i++)
        {
            var b = JsonSerializer.Serialize(before[i].Value);
            var a = JsonSerializer.Serialize(after[i].Value);
            if (b != a)
                diff.Add(new { field = before[i].Key, before = before[i].Value, after = after[i].Value });
        }
        return diff;
    }
}
