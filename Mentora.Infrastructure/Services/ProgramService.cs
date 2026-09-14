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
    IValidator<UpdateProgramRequest> updateValidator,
    IValidator<UpdateProgramSessionCompletionRequest> completionValidator) : IProgramService
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
        // Write rule: only rows owned by this coach — another coach's program 404s, never 403.
        // Ownership wins over body validation: an invalid body on someone else's program must
        // still 404.
        var program = await db.Programs
            .FirstOrDefaultAsync(p => p.ProgramId == programId && p.ProgramCoachId == coachId, ct)
            ?? throw new NotFoundException($"Program {programId} not found.");

        var validationContext = new ValidationContext<UpdateProgramRequest>(request);
        validationContext.RootContextData["CoachId"] = coachId;

        var validationResult = await updateValidator.ValidateAsync(validationContext, ct);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

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

    // ── Coach-side — completion (Lot 6.6) ─────────────────────────────────────────

    public async Task<ProgramSessionResponse> UpdateCompletionByCoachAsync(
        Guid coachId, Guid programSessionId, UpdateProgramSessionCompletionRequest request, CancellationToken ct)
    {
        // Ownership folded into the query filter — never 403. Ownership wins over body
        // validation: an invalid body on someone else's program session must still 404.
        var programSession = await db.ProgramSessions
            .FirstOrDefaultAsync(ps => ps.ProgramSessionId == programSessionId && ps.ProgramSessionCoachId == coachId, ct)
            ?? throw new NotFoundException($"Program session {programSessionId} not found.");

        // The MEMBER_COACHES link must exist too — a coach can still own old PROGRAM_SESSIONS rows
        // for a member who has since been unlinked, and that must 404 the same as a foreign one.
        var isLinked = await db.MemberCoaches
            .AnyAsync(mc => mc.MemberId == programSession.ProgramSessionMemberId && mc.CoachId == coachId, ct);
        if (!isLinked)
            throw new NotFoundException($"Program session {programSessionId} not found.");

        await completionValidator.ValidateAndThrowAsync(request, ct);

        await EnsureProgramActiveAsync(programSession.ProgramSessionProgramId, ct);

        return await ApplyCompletionAsync(programSession, request, allowCoachNote: true, ct);
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

    // ── Member-side — completion (Lot 6.6) ────────────────────────────────────────

    public async Task<ProgramSessionResponse> UpdateCompletionByMemberAsync(
        Guid memberId, Guid programSessionId, UpdateProgramSessionCompletionRequest request, CancellationToken ct)
    {
        // Ownership folded into the query filter — never 403. Ownership wins over body
        // validation: an invalid body on someone else's program session must still 404.
        var programSession = await db.ProgramSessions
            .FirstOrDefaultAsync(ps => ps.ProgramSessionId == programSessionId && ps.ProgramSessionMemberId == memberId, ct)
            ?? throw new NotFoundException($"Program session {programSessionId} not found.");

        await completionValidator.ValidateAndThrowAsync(request, ct);

        await EnsureProgramActiveAsync(programSession.ProgramSessionProgramId, ct);

        return await ApplyCompletionAsync(programSession, request, allowCoachNote: false, ct);
    }

    // ── Completion — shared by both endpoints (Lot 6.6) ───────────────────────────

    private async Task EnsureProgramActiveAsync(Guid programId, CancellationToken ct)
    {
        var status = await db.Programs
            .Where(p => p.ProgramId == programId)
            .Select(p => p.ProgramStatus)
            .FirstAsync(ct);

        if (status != ProgramStatus.Active)
            throw new ConflictException(
                "This program is not active — completions can only be recorded on an active program.",
                new { programStatus = EnumMappings.ProgramStatusMapping.ToWire(status) });
    }

    // One method taking the already-ownership-resolved ProgramSession, shared by both the coach
    // and member entry points above — they differ only in how they resolve and check ownership.
    private async Task<ProgramSessionResponse> ApplyCompletionAsync(
        ProgramSession programSession, UpdateProgramSessionCompletionRequest request, bool allowCoachNote, CancellationToken ct)
    {
        // c. Resolve every exercise belonging to this session in ONE query. This is also the full
        // set that full-replacement operates over: anything not in the payload gets cleared below.
        var sessionExercises = await (
            from pe in db.ProgramExercises
            join pc in db.ProgramCircuits on pe.ProgramExerciseCircuitId equals pc.ProgramCircuitId
            where pc.ProgramCircuitProgramSessionId == programSession.ProgramSessionId
            select pe).ToListAsync(ct);

        var validIds = sessionExercises.Select(e => e.ProgramExerciseId).ToHashSet();
        var payloadByExerciseId = request.Exercises.ToDictionary(e => e.ProgramExerciseId);

        var invalidIds = payloadByExerciseId.Keys.Where(id => !validIds.Contains(id)).ToList();
        if (invalidIds.Count > 0)
            throw new ValidationException(invalidIds.Select(id => new FluentValidation.Results.ValidationFailure(
                nameof(UpdateProgramSessionCompletionRequest.Exercises),
                $"ProgramExerciseId {id} does not belong to this program session.")));

        // e. actualWeightKg only allowed when LoadType is KG.
        var loadTypeFailures = sessionExercises
            .Where(e => payloadByExerciseId.TryGetValue(e.ProgramExerciseId, out var input)
                     && input.ActualWeightKg is not null
                     && e.ProgramExerciseLoadType != "KG")
            .Select(e => new FluentValidation.Results.ValidationFailure(
                nameof(UpdateProgramSessionCompletionRequest.Exercises),
                $"ProgramExerciseId {e.ProgramExerciseId}: actualWeightKg is only allowed when LoadType is KG " +
                $"(this exercise is {e.ProgramExerciseLoadType})."))
            .ToList();
        if (loadTypeFailures.Count > 0)
            throw new ValidationException(loadTypeFailures);

        // g. THE PRESCRIBED COLUMNS ARE NEVER TOUCHED BELOW — only ACTUAL_* and the per-exercise
        // MEMBER_FEEDBACK are written. The gap between prescribed and actual is the whole point of
        // this lot; see also BuildExerciseResponses, which reads both side by side.
        foreach (var exercise in sessionExercises)
        {
            if (payloadByExerciseId.TryGetValue(exercise.ProgramExerciseId, out var input))
            {
                exercise.ProgramExerciseActualSets     = input.ActualSets;
                exercise.ProgramExerciseActualReps     = input.ActualReps;
                exercise.ProgramExerciseActualWeightKg = input.ActualWeightKg;
                exercise.ProgramExerciseActualRpe      = input.ActualRpe;
                exercise.ProgramExerciseMemberFeedback = input.MemberFeedback;
            }
            else
            {
                // Full replacement: an exercise omitted from the payload has its actuals cleared,
                // not left alone.
                exercise.ProgramExerciseActualSets     = null;
                exercise.ProgramExerciseActualReps     = null;
                exercise.ProgramExerciseActualWeightKg = null;
                exercise.ProgramExerciseActualRpe      = null;
                exercise.ProgramExerciseMemberFeedback = null;
            }
        }

        var status = EnumMappings.ProgramSessionStatusMapping.Parse(request.Status);
        programSession.ProgramSessionStatus = status;
        // f. DONE stamps COMPLETED_DATE; PLANNED/SKIPPED clear it — but never touch the actuals
        // above. Undoing a completion must never destroy what the member typed.
        programSession.ProgramSessionCompletedDate = status == ProgramSessionStatus.Done ? DateTime.UtcNow : null;
        programSession.ProgramSessionMemberFeedback = request.MemberFeedback;
        if (allowCoachNote)
            programSession.ProgramSessionCoachNote = request.CoachNote;

        await db.SaveChangesAsync(ct);

        return await BuildSessionResponseAsync(programSession, ct);
    }

    // Single-session counterpart to BuildResponseAsync/AssembleTree, reusing the same
    // BuildCircuitResponses/BuildExerciseResponses helpers — the completion endpoints return just
    // the session that changed, not the whole program tree.
    private async Task<ProgramSessionResponse> BuildSessionResponseAsync(ProgramSession programSession, CancellationToken ct)
    {
        var program = await db.Programs.AsNoTracking()
            .FirstAsync(p => p.ProgramId == programSession.ProgramSessionProgramId, ct);
        var block = await db.ProgramBlocks.AsNoTracking()
            .FirstAsync(b => b.ProgramBlockId == programSession.ProgramSessionBlockId, ct);

        var circuits = await db.ProgramCircuits.AsNoTracking()
            .Where(c => c.ProgramCircuitProgramSessionId == programSession.ProgramSessionId)
            .ToListAsync(ct);
        var circuitIds = circuits.Select(c => c.ProgramCircuitId).ToList();
        var exercises = circuitIds.Count > 0
            ? await db.ProgramExercises.AsNoTracking().Where(e => circuitIds.Contains(e.ProgramExerciseCircuitId)).ToListAsync(ct)
            : [];

        var exerciseIds = exercises.Select(e => e.ProgramExerciseExerciseId).Distinct().ToList();
        var exerciseDetails = exerciseIds.Count > 0
            ? await db.Exercises.AsNoTracking().Where(e => exerciseIds.Contains(e.ExerciseId)).ToDictionaryAsync(e => e.ExerciseId, ct)
            : new Dictionary<Guid, Exercise>();

        var lastPerformedByExercise = await ResolveLastPerformedAsync(
            exercises.Select(e => e.ProgramExerciseId).ToList(), ct);

        var sessionDatesById = new Dictionary<Guid, DateTime>();
        if (programSession.ProgramSessionSessionId is { } linkedId)
        {
            var scheduledAt = await db.Sessions.AsNoTracking()
                .Where(s => s.SessionId == linkedId)
                .Select(s => s.SessionScheduledAt)
                .FirstOrDefaultAsync(ct);
            sessionDatesById[linkedId] = scheduledAt;
        }

        var circuitsBySession  = circuits.ToLookup(c => c.ProgramCircuitProgramSessionId);
        var exercisesByCircuit = exercises.ToLookup(e => e.ProgramExerciseCircuitId);

        return new ProgramSessionResponse(
            programSession.ProgramSessionId,
            programSession.ProgramSessionName,
            programSession.ProgramSessionType,
            programSession.ProgramSessionDayOfWeek,
            programSession.ProgramSessionPosition,
            EnumMappings.ProgramSessionStatusMapping.ToWire(programSession.ProgramSessionStatus),
            ComputeSessionDate(program, block.ProgramBlockWeekNumber, programSession.ProgramSessionDayOfWeek, programSession.ProgramSessionSessionId, sessionDatesById),
            programSession.ProgramSessionCompletedDate,
            programSession.ProgramSessionMemberFeedback,
            programSession.ProgramSessionCoachNote,
            BuildCircuitResponses(programSession.ProgramSessionId, circuitsBySession, exercisesByCircuit, exerciseDetails, lastPerformedByExercise));
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

        // LAST PERFORMED (Lot 6.6): one extra query for the whole tree, resolving every exercise's
        // most recent DONE record in one round trip — see ResolveLastPerformedAsync.
        var lastPerformedByExercise = await ResolveLastPerformedAsync(
            exercises.Select(e => e.ProgramExerciseId).ToList(), ct);

        return AssembleTree(program, blocks, sessions, circuits, exercises, exerciseDetails, sessionDatesById, lastPerformedByExercise);
    }

    private static ProgramResponse AssembleTree(
        Program program, List<ProgramBlock> blocks, List<ProgramSession> sessions,
        List<ProgramCircuit> circuits, List<ProgramExercise> exercises, Dictionary<Guid, Exercise> exerciseDetails,
        IReadOnlyDictionary<Guid, DateTime> sessionDatesById,
        IReadOnlyDictionary<Guid, LastPerformedResponse> lastPerformedByExercise)
    {
        // Dictionary<Guid?, T> rejects a null key at runtime even though the CLR type allows it,
        // so the "no parent" (root) group is keyed on Guid.Empty instead of null.
        var blocksByParent     = blocks.GroupBy(b => b.ProgramBlockParentId ?? Guid.Empty).ToDictionary(g => g.Key, g => g.OrderBy(b => b.ProgramBlockPosition).ToList());
        var sessionsByBlock    = sessions.GroupBy(s => s.ProgramSessionBlockId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.ProgramSessionPosition).ToList());
        var circuitsBySession  = circuits.ToLookup(c => c.ProgramCircuitProgramSessionId);
        var exercisesByCircuit = exercises.ToLookup(e => e.ProgramExerciseCircuitId);

        List<ProgramSessionResponse> BuildSessions(ProgramBlock block) =>
            sessionsByBlock.TryGetValue(block.ProgramBlockId, out var list)
                ? list.Select(s => new ProgramSessionResponse(
                    s.ProgramSessionId, s.ProgramSessionName, s.ProgramSessionType, s.ProgramSessionDayOfWeek,
                    s.ProgramSessionPosition, EnumMappings.ProgramSessionStatusMapping.ToWire(s.ProgramSessionStatus),
                    ComputeSessionDate(program, block.ProgramBlockWeekNumber, s.ProgramSessionDayOfWeek, s.ProgramSessionSessionId, sessionDatesById),
                    s.ProgramSessionCompletedDate, s.ProgramSessionMemberFeedback, s.ProgramSessionCoachNote,
                    BuildCircuitResponses(s.ProgramSessionId, circuitsBySession, exercisesByCircuit, exerciseDetails, lastPerformedByExercise))).ToList()
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

    // ── Circuit/exercise response building (shared by the whole-tree assembly and the single-
    // session response the completion endpoints return — Lot 6.6) ──────────────────────────────

    private static List<ProgramExerciseResponse> BuildExerciseResponses(
        Guid circuitId, ILookup<Guid, ProgramExercise> exercisesByCircuit, IReadOnlyDictionary<Guid, Exercise> exerciseDetails,
        IReadOnlyDictionary<Guid, LastPerformedResponse> lastPerformedByExercise) =>
        exercisesByCircuit[circuitId]
            .OrderBy(e => e.ProgramExercisePosition)
            .Select(e =>
            {
                exerciseDetails.TryGetValue(e.ProgramExerciseExerciseId, out var detail);
                lastPerformedByExercise.TryGetValue(e.ProgramExerciseId, out var lastPerformed);
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
                    // THE PRESCRIBED COLUMNS ABOVE ARE NEVER WRITTEN BY THE COMPLETION ENDPOINTS —
                    // only read here, alongside the actuals below. The gap between the two is the
                    // whole point of this lot.
                    e.ProgramExerciseActualSets,
                    e.ProgramExerciseActualReps,
                    e.ProgramExerciseActualWeightKg,
                    e.ProgramExerciseActualRpe,
                    e.ProgramExerciseMemberFeedback,
                    lastPerformed);
            }).ToList();

    private static List<ProgramCircuitResponse> BuildCircuitResponses(
        Guid sessionId, ILookup<Guid, ProgramCircuit> circuitsBySession, ILookup<Guid, ProgramExercise> exercisesByCircuit,
        IReadOnlyDictionary<Guid, Exercise> exerciseDetails, IReadOnlyDictionary<Guid, LastPerformedResponse> lastPerformedByExercise) =>
        circuitsBySession[sessionId]
            .OrderBy(c => c.ProgramCircuitPosition)
            .Select(c => new ProgramCircuitResponse(
                c.ProgramCircuitId, c.ProgramCircuitName, c.ProgramCircuitPosition, c.ProgramCircuitMode,
                c.ProgramCircuitRounds, c.ProgramCircuitRestBetweenRoundsSeconds, c.ProgramCircuitNote,
                BuildExerciseResponses(c.ProgramCircuitId, exercisesByCircuit, exerciseDetails, lastPerformedByExercise)))
            .ToList();

    // Most recent DONE record of the SAME EXERCISE_ID by the SAME member, across any program,
    // excluding the occurrence's own session — resolved for every requested ProgramExercise in ONE
    // query. `occurrences` (ProgramExercise joined to its owning ProgramSession) is reused as both
    // the outer set (the rows we need lastPerformed for) and, correlated per row, the inner
    // candidate set — EF Core composes this into a single SQL statement with a scalar correlated
    // subquery per outer row (Postgres's LATERAL-equivalent), never one round trip per exercise.
    private async Task<Dictionary<Guid, LastPerformedResponse>> ResolveLastPerformedAsync(
        List<Guid> programExerciseIds, CancellationToken ct)
    {
        if (programExerciseIds.Count == 0)
            return new Dictionary<Guid, LastPerformedResponse>();

        var occurrences =
            from pe in db.ProgramExercises
            join pc in db.ProgramCircuits on pe.ProgramExerciseCircuitId equals pc.ProgramCircuitId
            join ps in db.ProgramSessions on pc.ProgramCircuitProgramSessionId equals ps.ProgramSessionId
            select new { pe, ps };

        var rows = await occurrences
            .Where(x => programExerciseIds.Contains(x.pe.ProgramExerciseId))
            .Select(x => new
            {
                x.pe.ProgramExerciseId,
                Last = occurrences
                    .Where(y => y.pe.ProgramExerciseExerciseId == x.pe.ProgramExerciseExerciseId
                             && y.ps.ProgramSessionMemberId == x.ps.ProgramSessionMemberId
                             && y.ps.ProgramSessionStatus == ProgramSessionStatus.Done
                             && y.ps.ProgramSessionId != x.ps.ProgramSessionId)
                    .OrderByDescending(y => y.ps.ProgramSessionCompletedDate)
                    .Select(y => new
                    {
                        Date = y.ps.ProgramSessionCompletedDate,
                        y.pe.ProgramExerciseActualSets,
                        y.pe.ProgramExerciseActualReps,
                        y.pe.ProgramExerciseActualWeightKg,
                        y.pe.ProgramExerciseActualRpe
                    })
                    .FirstOrDefault()
            })
            .AsNoTracking()
            .ToListAsync(ct);

        return rows
            .Where(r => r.Last is not null)
            .ToDictionary(
                r => r.ProgramExerciseId,
                r => new LastPerformedResponse(
                    r.Last!.Date!.Value, r.Last.ProgramExerciseActualSets, r.Last.ProgramExerciseActualReps,
                    r.Last.ProgramExerciseActualWeightKg, r.Last.ProgramExerciseActualRpe));
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
