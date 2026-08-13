using System.Data;
using System.Text.Json;
using FluentValidation;
using Mentora.Core.DTOs.Program;
using Mentora.Core.DTOs.ProgramTemplate.Body;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class ProgramService(
    MentoraDbContext db,
    IValidator<ProgramTemplateBody> bodyValidator,
    IValidator<UpdateProgramRequest> updateValidator) : IProgramService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ── Coach-side ─────────────────────────────────────────────────────────────

    public async Task<List<ProgramHeaderResponse>> ListForCoachAsync(
        Guid coachId, Guid memberId, bool includeArchived, CancellationToken ct)
    {
        var isLinked = await db.MemberCoaches.AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);
        if (!isLinked)
            throw new NotFoundException($"Member {memberId} not found.");

        var query = db.Programs.Where(p => p.ProgramMemberId == memberId && p.ProgramCoachId == coachId);

        if (!includeArchived)
            query = query.Where(p => p.ProgramStatus != ProgramStatus.Archived);

        return await query
            .OrderByDescending(p => p.ProgramCreatedDate)
            .Select(p => ToHeaderResponse(p))
            .ToListAsync(ct);
    }

    public async Task<ProgramResponse> GetByIdForCoachAsync(Guid coachId, Guid programId, CancellationToken ct)
    {
        var program = await db.Programs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProgramId == programId && p.ProgramCoachId == coachId, ct)
            ?? throw new NotFoundException($"Program {programId} not found.");

        return await BuildResponseAsync(program, ct);
    }

    public async Task<AssignProgramResponse> AssignAsync(
        Guid coachId, Guid memberId, AssignProgramRequest request, CancellationToken ct)
    {
        // a. Verify the MEMBER_COACHES link first — 404, never 403. A 403 would confirm the
        // member exists under another coach.
        var isLinked = await db.MemberCoaches.AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);
        if (!isLinked)
            throw new NotFoundException($"Member {memberId} not found.");

        string name;
        ProgramGoal goal;
        int durationWeeks;
        ProgramTemplateBody body;

        if (request.TemplateId is { } templateId)
        {
            var template = await db.ProgramTemplates
                .FirstOrDefaultAsync(t => t.ProgramTemplateId == templateId
                                        && (t.ProgramTemplateCoachId == null || t.ProgramTemplateCoachId == coachId)
                                        && t.ProgramTemplateIsActive, ct)
                ?? throw new NotFoundException($"Program template {templateId} not found.");

            name          = template.ProgramTemplateName;
            goal          = template.ProgramTemplateGoal;
            durationWeeks = template.ProgramTemplateDurationWeeks;
            body          = JsonSerializer.Deserialize<ProgramTemplateBody>(template.ProgramTemplateBody, JsonOptions)!;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("Name is required when templateId is not supplied.");
            if (string.IsNullOrWhiteSpace(request.Goal))
                throw new InvalidOperationException("Goal is required when templateId is not supplied.");
            if (!EnumMappings.ProgramGoalMapping.WireValues.Contains(request.Goal))
                throw new InvalidOperationException(
                    $"Invalid goal '{request.Goal}'. Accepted values: {string.Join(", ", EnumMappings.ProgramGoalMapping.WireValues)}.");
            if (request.DurationWeeks is not > 0)
                throw new InvalidOperationException("DurationWeeks is required and must be positive when templateId is not supplied.");
            if (request.Body is null)
                throw new InvalidOperationException("Body is required when templateId is not supplied.");

            name          = request.Name.Trim();
            goal          = EnumMappings.ProgramGoalMapping.Parse(request.Goal);
            durationWeeks = request.DurationWeeks.Value;
            body          = request.Body;

            await ValidateBodyTreeAsync(body, durationWeeks, coachId, ct);
        }

        // b. Inside ONE Serializable transaction: archive the member's current ACTIVE program
        // (if any), then insert the new tree.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var currentActive = await db.Programs
            .FirstOrDefaultAsync(p => p.ProgramMemberId == memberId && p.ProgramStatus == ProgramStatus.Active, ct);

        Guid? archivedProgramId = null;
        if (currentActive is not null)
        {
            currentActive.ProgramStatus      = ProgramStatus.Archived;
            currentActive.ProgramUpdatedDate = DateTime.UtcNow;
            archivedProgramId                = currentActive.ProgramId;
        }

        var now = DateTime.UtcNow;
        var program = new Program
        {
            ProgramId            = Guid.NewGuid(),
            ProgramCoachId       = coachId,
            ProgramMemberId      = memberId,
            ProgramTemplateId    = request.TemplateId,
            ProgramName          = name,
            ProgramGoal          = goal,
            ProgramStartDate     = request.StartDate,
            ProgramDurationWeeks = durationWeeks,
            ProgramWeekOffset    = 0,
            ProgramStatus        = ProgramStatus.Active,
            ProgramCreatedDate   = now,
            ProgramUpdatedDate   = now,
        };
        db.Programs.Add(program);

        // c/d. Deep, independent copy — only ExerciseId pointers are kept, never the exercise
        // content itself, so editing/soft-deleting the source template or an exercise afterwards
        // has zero effect on this program.
        var blocks    = new List<ProgramBlock>();
        var sessions  = new List<ProgramSession>();
        var circuits  = new List<ProgramCircuit>();
        var exercises = new List<ProgramExercise>();
        CopyBlockTree(program.ProgramId, coachId, memberId, body.Blocks, null, blocks, sessions, circuits, exercises);

        db.ProgramBlocks.AddRange(blocks);
        db.ProgramSessions.AddRange(sessions);
        db.ProgramCircuits.AddRange(circuits);
        db.ProgramExercises.AddRange(exercises);

        // e. Single SaveChanges for the whole tree — no per-row round trip.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var response = await BuildResponseAsync(program, ct);
        return new AssignProgramResponse(response, archivedProgramId);
    }

    public async Task<ProgramResponse> UpdateAsync(
        Guid coachId, Guid programId, UpdateProgramRequest request, CancellationToken ct)
    {
        var validationContext = new ValidationContext<UpdateProgramRequest>(request);
        validationContext.RootContextData["CoachId"] = coachId;

        var validationResult = await updateValidator.ValidateAsync(validationContext, ct);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        // Write rule: only rows owned by this coach — another coach's program 404s, never 403.
        var program = await db.Programs
            .FirstOrDefaultAsync(p => p.ProgramId == programId && p.ProgramCoachId == coachId, ct)
            ?? throw new NotFoundException($"Program {programId} not found.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        // Full replacement: delete the existing tree (children first) and reinsert, in one
        // transaction. Never partial.
        var existingExercises = await db.ProgramExercises.Where(e => e.ProgramExerciseProgramId == programId).ToListAsync(ct);
        var existingCircuits   = await db.ProgramCircuits.Where(c => c.ProgramCircuitProgramId == programId).ToListAsync(ct);
        var existingSessions   = await db.ProgramSessions.Where(s => s.ProgramSessionProgramId == programId).ToListAsync(ct);
        var existingBlocks     = await db.ProgramBlocks.Where(b => b.ProgramBlockProgramId == programId).ToListAsync(ct);

        db.ProgramExercises.RemoveRange(existingExercises);
        db.ProgramCircuits.RemoveRange(existingCircuits);
        db.ProgramSessions.RemoveRange(existingSessions);
        db.ProgramBlocks.RemoveRange(existingBlocks);

        program.ProgramName          = request.Name.Trim();
        program.ProgramGoal          = EnumMappings.ProgramGoalMapping.Parse(request.Goal);
        program.ProgramStartDate     = request.StartDate;
        program.ProgramDurationWeeks = request.DurationWeeks;
        program.ProgramUpdatedDate   = DateTime.UtcNow;

        var blocks    = new List<ProgramBlock>();
        var sessions  = new List<ProgramSession>();
        var circuits  = new List<ProgramCircuit>();
        var exercises = new List<ProgramExercise>();
        CopyBlockTree(programId, coachId, program.ProgramMemberId, request.Body.Blocks, null, blocks, sessions, circuits, exercises);

        db.ProgramBlocks.AddRange(blocks);
        db.ProgramSessions.AddRange(sessions);
        db.ProgramCircuits.AddRange(circuits);
        db.ProgramExercises.AddRange(exercises);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await BuildResponseAsync(program, ct);
    }

    public async Task DeleteAsync(Guid coachId, Guid programId, CancellationToken ct)
    {
        var program = await db.Programs
            .FirstOrDefaultAsync(p => p.ProgramId == programId && p.ProgramCoachId == coachId, ct)
            ?? throw new NotFoundException($"Program {programId} not found.");

        program.ProgramStatus      = ProgramStatus.Archived;
        program.ProgramUpdatedDate = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    // ── Coach-side — booking correction (Lot 6.5) ─────────────────────────────────

    public async Task<ProgramSessionBookingResponse> UpdateBookingAsync(
        Guid coachId, Guid programSessionId, UpdateProgramSessionBookingRequest request, CancellationToken ct)
    {
        // Ownership folded into the query filter — never 403.
        var programSession = await db.ProgramSessions
            .FirstOrDefaultAsync(ps => ps.ProgramSessionId == programSessionId && ps.ProgramSessionCoachId == coachId, ct)
            ?? throw new NotFoundException($"Program session {programSessionId} not found.");

        if (request.SessionId is { } sessionId)
        {
            var session = await db.Sessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.SessionCoachId == coachId, ct)
                ?? throw new NotFoundException($"Session {sessionId} not found.");

            // The session's member (individual) or participant list (group) must include the
            // program session's member — via V_SESSION_MEMBERS, same unified path as Lot 6.4.
            // A mismatch is an ownership-adjacent failure, not a data problem: 404, never 403.
            var sessionHasMember = await db.SessionMembers
                .AnyAsync(v => v.SessionId == sessionId && v.MemberId == programSession.ProgramSessionMemberId, ct);
            if (!sessionHasMember)
                throw new NotFoundException($"Session {sessionId} not found.");

            var sessionOfferTypeWire = EnumMappings.OfferTypeMapping.ToWire(session.SessionOfferType);
            if (sessionOfferTypeWire != programSession.ProgramSessionType)
                throw new ConflictException(
                    "Session and program session types do not match.",
                    new { sessionType = sessionOfferTypeWire, programSessionType = programSession.ProgramSessionType });

            // Defensive: IX_PROGRAM_SESSIONS_BOOKING_MEMBER is unique on (SESSION_ID, MEMBER_ID).
            // Catch the conflict here with a clear 409 rather than let a raw constraint violation
            // surface as 500 — this member may already have a different program session linked to
            // that same booking (e.g. a duplicate correction attempt).
            var alreadyLinked = await db.ProgramSessions.AnyAsync(ps =>
                ps.ProgramSessionId != programSessionId &&
                ps.ProgramSessionSessionId == sessionId &&
                ps.ProgramSessionMemberId == programSession.ProgramSessionMemberId, ct);
            if (alreadyLinked)
                throw new ConflictException("This member already has a program session linked to that booking.");

            programSession.ProgramSessionSessionId = sessionId;
        }
        else
        {
            // Detach only clears the link — the booking itself (SESSIONS row, voucher, participant
            // registration) is left completely untouched.
            programSession.ProgramSessionSessionId = null;
        }

        await db.SaveChangesAsync(ct);

        return new ProgramSessionBookingResponse(
            programSession.ProgramSessionId,
            programSession.ProgramSessionSessionId,
            programSession.ProgramSessionName,
            programSession.ProgramSessionType,
            EnumMappings.ProgramSessionStatusMapping.ToWire(programSession.ProgramSessionStatus));
    }

    // ── Member-side ────────────────────────────────────────────────────────────

    public async Task<ProgramResponse> GetCurrentForMemberAsync(Guid memberId, CancellationToken ct)
    {
        var program = await db.Programs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProgramMemberId == memberId && p.ProgramStatus == ProgramStatus.Active, ct)
            ?? throw new NotFoundException("No active program found.");

        return await BuildResponseAsync(program, ct);
    }

    public async Task<ProgramResponse> GetByIdForMemberAsync(Guid memberId, Guid programId, CancellationToken ct)
    {
        var program = await db.Programs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProgramId == programId && p.ProgramMemberId == memberId, ct)
            ?? throw new NotFoundException($"Program {programId} not found.");

        return await BuildResponseAsync(program, ct);
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    private async Task ValidateBodyTreeAsync(ProgramTemplateBody body, int durationWeeks, Guid coachId, CancellationToken ct)
    {
        var validationContext = new ValidationContext<ProgramTemplateBody>(body);
        validationContext.RootContextData["DurationWeeks"] = durationWeeks;
        validationContext.RootContextData["CoachId"]       = coachId;

        var result = await bodyValidator.ValidateAsync(validationContext, ct);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);
    }

    // ── Tree copy (deep, independent — only ExerciseId pointers survive) ────────

    private static void CopyBlockTree(
        Guid programId, Guid coachId, Guid memberId, List<ProgramTemplateBlock> templateBlocks, Guid? parentId,
        List<ProgramBlock> blocks, List<ProgramSession> sessions, List<ProgramCircuit> circuits, List<ProgramExercise> exercises)
    {
        foreach (var tb in templateBlocks)
        {
            var blockId = Guid.NewGuid();
            blocks.Add(new ProgramBlock
            {
                ProgramBlockId         = blockId,
                ProgramBlockProgramId  = programId,
                ProgramBlockParentId   = parentId,
                ProgramBlockLevel      = tb.Level,
                ProgramBlockName       = tb.Name,
                ProgramBlockPosition   = tb.Position,
                ProgramBlockWeekNumber = tb.WeekNumber,
            });

            foreach (var ts in tb.Sessions)
            {
                var sessionId = Guid.NewGuid();
                sessions.Add(new ProgramSession
                {
                    ProgramSessionId        = sessionId,
                    ProgramSessionProgramId = programId,
                    ProgramSessionBlockId   = blockId,
                    ProgramSessionCoachId   = coachId,
                    ProgramSessionMemberId  = memberId,
                    ProgramSessionSessionId = null,
                    ProgramSessionName      = ts.Name,
                    ProgramSessionType      = ts.Type,
                    ProgramSessionDayOfWeek = ts.DayOfWeek,
                    ProgramSessionPosition  = ts.Position,
                    ProgramSessionStatus    = ProgramSessionStatus.Planned,
                });

                foreach (var tc in ts.Circuits)
                {
                    var circuitId = Guid.NewGuid();
                    circuits.Add(new ProgramCircuit
                    {
                        ProgramCircuitId                       = circuitId,
                        ProgramCircuitProgramId                = programId,
                        ProgramCircuitProgramSessionId         = sessionId,
                        ProgramCircuitName                     = tc.Name,
                        ProgramCircuitPosition                 = tc.Position,
                        ProgramCircuitMode                     = tc.Mode,
                        ProgramCircuitRounds                   = tc.Rounds,
                        ProgramCircuitRestBetweenRoundsSeconds = tc.RestBetweenRoundsSeconds,
                        ProgramCircuitNote                     = tc.Note,
                    });

                    foreach (var te in tc.Exercises)
                    {
                        exercises.Add(new ProgramExercise
                        {
                            ProgramExerciseId                 = Guid.NewGuid(),
                            ProgramExerciseProgramId          = programId,
                            ProgramExerciseCircuitId          = circuitId,
                            ProgramExerciseExerciseId         = te.ExerciseId,
                            ProgramExercisePosition           = te.Position,
                            ProgramExerciseLoadType           = te.LoadType,
                            ProgramExerciseCustomNote         = te.CustomNote,
                            ProgramExercisePrescribedSets     = te.PrescribedSets,
                            ProgramExercisePrescribedReps     = te.PrescribedReps,
                            ProgramExercisePrescribedWeightKg = te.PrescribedWeightKg,
                            ProgramExerciseRestSeconds        = te.RestSeconds,
                            ProgramExerciseWorkSeconds        = te.WorkSeconds,
                            ProgramExerciseRestWorkSeconds    = te.RestWorkSeconds,
                        });
                    }
                }
            }

            if (tb.Blocks is { Count: > 0 })
                CopyBlockTree(programId, coachId, memberId, tb.Blocks, blockId, blocks, sessions, circuits, exercises);
        }
    }

    // ── Tree read + assembly (five flat queries, assembled in memory — no Include, no N+1) ────

    private async Task<ProgramResponse> BuildResponseAsync(Program program, CancellationToken ct)
    {
        var programId = program.ProgramId;

        var blocks    = await db.ProgramBlocks.AsNoTracking().Where(b => b.ProgramBlockProgramId == programId).ToListAsync(ct);
        var sessions  = await db.ProgramSessions.AsNoTracking().Where(s => s.ProgramSessionProgramId == programId).ToListAsync(ct);
        var circuits  = await db.ProgramCircuits.AsNoTracking().Where(c => c.ProgramCircuitProgramId == programId).ToListAsync(ct);
        var exercises = await db.ProgramExercises.AsNoTracking().Where(e => e.ProgramExerciseProgramId == programId).ToListAsync(ct);

        // Resolve every referenced exercise in ONE query for the whole tree.
        var exerciseIds = exercises.Select(e => e.ProgramExerciseExerciseId).Distinct().ToList();
        var exerciseDetails = exerciseIds.Count > 0
            ? await db.Exercises.AsNoTracking().Where(e => exerciseIds.Contains(e.ExerciseId)).ToDictionaryAsync(e => e.ExerciseId, ct)
            : new Dictionary<Guid, Exercise>();

        // AUTHORITY RULE (Lot 6.5): once a session is linked, SESSIONS.SESSION_SCHEDULED_AT is the
        // authoritative date — the computed formula is ignored for it. One extra query, only when
        // at least one session in this tree is actually linked (the common case of a freshly
        // assigned, all-PLANNED program stays at the original 6 queries).
        var linkedSessionIds = sessions
            .Where(s => s.ProgramSessionSessionId.HasValue)
            .Select(s => s.ProgramSessionSessionId!.Value)
            .Distinct()
            .ToList();
        var sessionDatesById = linkedSessionIds.Count > 0
            ? await db.Sessions.AsNoTracking()
                .Where(s => linkedSessionIds.Contains(s.SessionId))
                .ToDictionaryAsync(s => s.SessionId, s => s.SessionScheduledAt, ct)
            : new Dictionary<Guid, DateTime>();

        return AssembleTree(program, blocks, sessions, circuits, exercises, exerciseDetails, sessionDatesById);
    }

    private static ProgramResponse AssembleTree(
        Program program, List<ProgramBlock> blocks, List<ProgramSession> sessions,
        List<ProgramCircuit> circuits, List<ProgramExercise> exercises, Dictionary<Guid, Exercise> exerciseDetails,
        IReadOnlyDictionary<Guid, DateTime> sessionDatesById)
    {
        // Dictionary<Guid?, T> rejects a null key at runtime even though the CLR type allows it,
        // so the "no parent" (root) group is keyed on Guid.Empty instead of null.
        var blocksByParent     = blocks.GroupBy(b => b.ProgramBlockParentId ?? Guid.Empty).ToDictionary(g => g.Key, g => g.OrderBy(b => b.ProgramBlockPosition).ToList());
        var sessionsByBlock    = sessions.GroupBy(s => s.ProgramSessionBlockId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.ProgramSessionPosition).ToList());
        var circuitsBySession  = circuits.GroupBy(c => c.ProgramCircuitProgramSessionId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.ProgramCircuitPosition).ToList());
        var exercisesByCircuit = exercises.GroupBy(e => e.ProgramExerciseCircuitId).ToDictionary(g => g.Key, g => g.OrderBy(e => e.ProgramExercisePosition).ToList());

        List<ProgramExerciseResponse> BuildExercises(Guid circuitId) =>
            exercisesByCircuit.TryGetValue(circuitId, out var list)
                ? list.Select(e =>
                {
                    exerciseDetails.TryGetValue(e.ProgramExerciseExerciseId, out var detail);
                    return new ProgramExerciseResponse(
                        e.ProgramExerciseId,
                        e.ProgramExerciseExerciseId,
                        detail?.ExerciseName ?? "(unavailable)",
                        detail?.ExerciseInstructions,
                        detail?.ExerciseVideoUrl,
                        detail?.ExerciseImageUrl,
                        detail is not null ? EnumMappings.MuscleGroupMapping.ToWire(detail.ExerciseMuscleGroup) : "(unavailable)",
                        detail is not null ? EnumMappings.EquipmentMapping.ToWire(detail.ExerciseEquipment) : "(unavailable)",
                        e.ProgramExercisePosition,
                        e.ProgramExerciseLoadType,
                        e.ProgramExerciseCustomNote,
                        e.ProgramExercisePrescribedSets,
                        e.ProgramExercisePrescribedReps,
                        e.ProgramExercisePrescribedWeightKg,
                        e.ProgramExerciseRestSeconds,
                        e.ProgramExerciseWorkSeconds,
                        e.ProgramExerciseRestWorkSeconds,
                        e.ProgramExerciseActualSets,
                        e.ProgramExerciseActualReps,
                        e.ProgramExerciseActualWeightKg,
                        e.ProgramExerciseActualRpe,
                        e.ProgramExerciseMemberFeedback);
                }).ToList()
                : [];

        List<ProgramCircuitResponse> BuildCircuits(Guid sessionId) =>
            circuitsBySession.TryGetValue(sessionId, out var list)
                ? list.Select(c => new ProgramCircuitResponse(
                    c.ProgramCircuitId, c.ProgramCircuitName, c.ProgramCircuitPosition, c.ProgramCircuitMode,
                    c.ProgramCircuitRounds, c.ProgramCircuitRestBetweenRoundsSeconds, c.ProgramCircuitNote,
                    BuildExercises(c.ProgramCircuitId))).ToList()
                : [];

        List<ProgramSessionResponse> BuildSessions(ProgramBlock block) =>
            sessionsByBlock.TryGetValue(block.ProgramBlockId, out var list)
                ? list.Select(s => new ProgramSessionResponse(
                    s.ProgramSessionId, s.ProgramSessionName, s.ProgramSessionType, s.ProgramSessionDayOfWeek,
                    s.ProgramSessionPosition, EnumMappings.ProgramSessionStatusMapping.ToWire(s.ProgramSessionStatus),
                    ComputeSessionDate(program, block.ProgramBlockWeekNumber, s.ProgramSessionDayOfWeek, s.ProgramSessionSessionId, sessionDatesById),
                    s.ProgramSessionCompletedDate, s.ProgramSessionMemberFeedback, s.ProgramSessionCoachNote,
                    BuildCircuits(s.ProgramSessionId))).ToList()
                : [];

        List<ProgramBlockResponse> BuildBlocks(Guid? parentId) =>
            blocksByParent.TryGetValue(parentId ?? Guid.Empty, out var list)
                ? list.Select(b => new ProgramBlockResponse(
                    b.ProgramBlockId, b.ProgramBlockParentId, b.ProgramBlockLevel, b.ProgramBlockName,
                    b.ProgramBlockPosition, b.ProgramBlockWeekNumber,
                    BuildBlocks(b.ProgramBlockId), BuildSessions(b))).ToList()
                : [];

        return new ProgramResponse(
            program.ProgramId,
            program.ProgramCoachId,
            program.ProgramMemberId,
            program.ProgramTemplateId,
            program.ProgramName,
            EnumMappings.ProgramGoalMapping.ToWire(program.ProgramGoal),
            program.ProgramStartDate,
            program.ProgramDurationWeeks,
            program.ProgramWeekOffset,
            EnumMappings.ProgramStatusMapping.ToWire(program.ProgramStatus),
            program.ProgramCreatedDate,
            program.ProgramUpdatedDate,
            BuildBlocks(null));
    }

    // startDate + (weekNumber - 1 + weekOffset) * 7 + (dayOfWeek - 1). weekNumber comes from the
    // parent MICROCYCLE block (sessions only ever exist under MICROCYCLE blocks — enforced by
    // ProgramTemplateBodyValidator and the PROGRAM_BLOCKS CHECK constraints — so it is always set).
    // Nothing is stored; this is recomputed on every read — EXCEPT when the session is linked to a
    // booking (Lot 6.5): AUTHORITY RULE — SESSIONS.SESSION_SCHEDULED_AT then wins outright, and the
    // formula below is never evaluated for it. Never copy one into the other.
    private static DateOnly ComputeSessionDate(
        Program program, int? weekNumber, int dayOfWeek, Guid? linkedSessionId,
        IReadOnlyDictionary<Guid, DateTime> sessionDatesById)
    {
        if (linkedSessionId is { } sessionId && sessionDatesById.TryGetValue(sessionId, out var scheduledAt))
            return DateOnly.FromDateTime(scheduledAt);

        return program.ProgramStartDate.AddDays((weekNumber!.Value - 1 + program.ProgramWeekOffset) * 7 + (dayOfWeek - 1));
    }

    private static ProgramHeaderResponse ToHeaderResponse(Program p) => new(
        p.ProgramId,
        p.ProgramCoachId,
        p.ProgramMemberId,
        p.ProgramTemplateId,
        p.ProgramName,
        EnumMappings.ProgramGoalMapping.ToWire(p.ProgramGoal),
        p.ProgramStartDate,
        p.ProgramDurationWeeks,
        EnumMappings.ProgramStatusMapping.ToWire(p.ProgramStatus),
        p.ProgramCreatedDate,
        p.ProgramUpdatedDate);
}
