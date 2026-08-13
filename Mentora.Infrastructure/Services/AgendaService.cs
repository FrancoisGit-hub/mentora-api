using FluentValidation;
using Mentora.Core.DTOs.Agenda;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class AgendaService(
    MentoraDbContext db,
    IValidator<CoachAgendaRequest> coachValidator,
    IValidator<MemberAgendaRequest> memberValidator) : IAgendaService
{
    // ── Coach-side ─────────────────────────────────────────────────────────────

    public async Task<List<AgendaEntryResponse>> GetForCoachAsync(
        Guid coachId, CoachAgendaRequest request, CancellationToken ct)
    {
        await coachValidator.ValidateAndThrowAsync(request, ct);
        var (fromDate, toDateExclusive) = ToDateBounds(request.From!.Value, request.To!.Value);

        var bookings = await db.Sessions
            .Where(s => s.SessionCoachId == coachId
                     && s.SessionScheduledAt >= fromDate && s.SessionScheduledAt < toDateExclusive)
            .AsNoTracking()
            .ToListAsync(ct);

        var bookingEntries = await BuildBookingEntriesAsync(bookings, forMemberId: null, ct);

        var entries = new List<AgendaEntryResponse>(bookingEntries);

        if (request.IncludeMemberStandalone)
        {
            var coachParameter = await db.CoachParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.CoachId == coachId, ct);
            var behavior = coachParameter?.CoachParameterMissedSessionBehavior ?? MissedSessionBehavior.Skip;

            var activePrograms = await db.Programs
                .Where(p => p.ProgramCoachId == coachId && p.ProgramStatus == ProgramStatus.Active)
                .AsNoTracking()
                .ToListAsync(ct);

            var standaloneEntries = await BuildStandaloneEntriesAsync(
                activePrograms, behavior, request.From.Value, request.To.Value, ct);
            entries.AddRange(standaloneEntries);
        }

        return entries.OrderBy(e => e.Date).ToList();
    }

    // ── Member-side ────────────────────────────────────────────────────────────

    public async Task<List<AgendaEntryResponse>> GetForMemberAsync(
        Guid memberId, MemberAgendaRequest request, CancellationToken ct)
    {
        await memberValidator.ValidateAndThrowAsync(request, ct);
        var (fromDate, toDateExclusive) = ToDateBounds(request.From!.Value, request.To!.Value);

        // Routed through V_SESSION_MEMBERS — covers both individual bookings and group
        // registrations, same unified path as Lot 6.4's session reads.
        var memberSessionIds = db.SessionMembers.Where(v => v.MemberId == memberId).Select(v => v.SessionId);

        var bookings = await db.Sessions
            .Where(s => memberSessionIds.Contains(s.SessionId)
                     && s.SessionScheduledAt >= fromDate && s.SessionScheduledAt < toDateExclusive)
            .AsNoTracking()
            .ToListAsync(ct);

        var bookingEntries = await BuildBookingEntriesAsync(bookings, forMemberId: memberId, ct);

        var entries = new List<AgendaEntryResponse>(bookingEntries);

        // Member agenda always includes standalone sessions — no flag, unlike the coach's.
        var activeProgram = await db.Programs
            .Where(p => p.ProgramMemberId == memberId && p.ProgramStatus == ProgramStatus.Active)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (activeProgram is not null)
        {
            var coachParameter = await db.CoachParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.CoachId == activeProgram.ProgramCoachId, ct);
            var behavior = coachParameter?.CoachParameterMissedSessionBehavior ?? MissedSessionBehavior.Skip;

            var standaloneEntries = await BuildStandaloneEntriesAsync(
                [activeProgram], behavior, request.From.Value, request.To.Value, ct);
            entries.AddRange(standaloneEntries);
        }

        return entries.OrderBy(e => e.Date).ToList();
    }

    // ── Booking entries (shared) ───────────────────────────────────────────────

    // forMemberId: null on the coach's agenda (group entries never populate ProgramSessionId —
    // one booking can carry several participants' program sessions, which a single scalar field
    // cannot represent; ParticipantCount conveys the multiplicity instead). Set to the caller's
    // own memberId on the member's agenda, where a group booking DOES populate ProgramSessionId —
    // filtered to that one member's own link, since a member has exactly one program session
    // behind any booking of theirs, individual or group.
    private async Task<List<AgendaEntryResponse>> BuildBookingEntriesAsync(
        List<Session> bookings, Guid? forMemberId, CancellationToken ct)
    {
        if (bookings.Count == 0)
            return [];

        var individualSessionIds = bookings.Where(b => b.SessionMemberId is not null).Select(b => b.SessionId).ToList();
        var groupSessionIds      = bookings.Where(b => b.SessionMemberId is null).Select(b => b.SessionId).ToList();

        // Individual bookings: the other party's name. Coach agenda shows the member; member
        // agenda shows the coach — "who you're meeting", same convention on both sides.
        var memberNamesById = new Dictionary<Guid, string>();
        var coachNamesById  = new Dictionary<Guid, string>();
        if (individualSessionIds.Count > 0)
        {
            if (forMemberId is null)
            {
                var memberIds = bookings.Where(b => b.SessionMemberId is not null)
                    .Select(b => b.SessionMemberId!.Value).Distinct().ToList();
                memberNamesById = await db.Members
                    .Where(m => memberIds.Contains(m.MemberId))
                    .ToDictionaryAsync(m => m.MemberId, m => $"{m.MemberFirstName} {m.MemberLastName}".Trim(), ct);
            }
            else
            {
                var coachIds = bookings.Where(b => b.SessionMemberId is not null)
                    .Select(b => b.SessionCoachId).Distinct().ToList();
                coachNamesById = await db.Coaches
                    .Where(c => coachIds.Contains(c.CoachId))
                    .ToDictionaryAsync(c => c.CoachId, c => $"{c.CoachFirstName} {c.CoachLastName}".Trim(), ct);
            }
        }

        // Group bookings: participant counts, batched for the whole page — no per-row query.
        var participantCountBySession = groupSessionIds.Count > 0
            ? await db.SessionParticipants
                .Where(p => groupSessionIds.Contains(p.SessionParticipantSessionId)
                         && p.SessionParticipantStatus != SessionParticipantStatus.Cancelled)
                .GroupBy(p => p.SessionParticipantSessionId)
                .Select(g => new { SessionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SessionId, x => x.Count, ct)
            : new Dictionary<Guid, int>();

        // Linked program session, batched:
        //  - individual bookings: at most one row per session (single member).
        //  - group bookings, member agenda only: this member's own row, if any.
        var programSessionIdBySession = new Dictionary<Guid, Guid>();
        if (individualSessionIds.Count > 0)
        {
            programSessionIdBySession = await db.ProgramSessions
                .Where(ps => ps.ProgramSessionSessionId != null && individualSessionIds.Contains(ps.ProgramSessionSessionId!.Value))
                .ToDictionaryAsync(ps => ps.ProgramSessionSessionId!.Value, ps => ps.ProgramSessionId, ct);
        }
        if (forMemberId is { } memberIdFilter && groupSessionIds.Count > 0)
        {
            var ownGroupLinks = await db.ProgramSessions
                .Where(ps => ps.ProgramSessionSessionId != null
                          && groupSessionIds.Contains(ps.ProgramSessionSessionId!.Value)
                          && ps.ProgramSessionMemberId == memberIdFilter)
                .ToDictionaryAsync(ps => ps.ProgramSessionSessionId!.Value, ps => ps.ProgramSessionId, ct);
            foreach (var (sessionId, programSessionId) in ownGroupLinks)
                programSessionIdBySession[sessionId] = programSessionId;
        }

        return bookings.Select(b =>
        {
            var isGroup = b.SessionMemberId is null;
            var participantCount = isGroup ? participantCountBySession.GetValueOrDefault(b.SessionId, 0) : (int?)null;

            string title;
            if (isGroup)
                title = $"Cours collectif ({participantCount} participant{(participantCount == 1 ? "" : "s")})";
            else if (forMemberId is null)
                title = memberNamesById.GetValueOrDefault(b.SessionMemberId!.Value, "(unavailable)");
            else
                title = coachNamesById.GetValueOrDefault(b.SessionCoachId, "(unavailable)");

            // Group booking on the coach's own agenda: no single program session to point to.
            var programSessionId = (isGroup && forMemberId is null)
                ? null
                : programSessionIdBySession.TryGetValue(b.SessionId, out var psId) ? psId : (Guid?)null;

            return new AgendaEntryResponse(
                Date:              b.SessionScheduledAt, // AUTHORITY RULE — never the computed formula
                Type:              EnumMappings.OfferTypeMapping.ToWire(b.SessionOfferType),
                Title:             title,
                Status:            EnumMappings.SessionStatusMapping.ToWire(ComputeEffectiveStatus(b)),
                IsBooked:          true,
                ProgramSessionId:  programSessionId,
                SessionId:         b.SessionId,
                ParticipantCount:  participantCount,
                IsOverdue:         false);
        }).ToList();
    }

    // ── Standalone entries (shared) ────────────────────────────────────────────

    private async Task<List<AgendaEntryResponse>> BuildStandaloneEntriesAsync(
        List<Program> activePrograms, MissedSessionBehavior behavior,
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (activePrograms.Count == 0)
            return [];

        var programIds = activePrograms.Select(p => p.ProgramId).ToList();

        // One query for every candidate program session across all these programs, carrying its
        // week number — reused both to compute each program's SHIFT (if applicable) and to build
        // the displayed entries. No N+1 regardless of how many active programs are in scope.
        var rows = await (
            from ps in db.ProgramSessions
            join pb in db.ProgramBlocks on ps.ProgramSessionBlockId equals pb.ProgramBlockId
            where programIds.Contains(ps.ProgramSessionProgramId) && pb.ProgramBlockWeekNumber != null
            select new { ps.ProgramSessionProgramId, ProgramSession = ps, WeekNumber = pb.ProgramBlockWeekNumber!.Value })
            .AsNoTracking()
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var entries = new List<AgendaEntryResponse>();

        foreach (var program in activePrograms)
        {
            var programRows = rows.Where(r => r.ProgramSessionProgramId == program.ProgramId).ToList();

            var weeksWithDone = programRows
                .Where(r => r.ProgramSession.ProgramSessionStatus == ProgramSessionStatus.Done)
                .Select(r => r.WeekNumber)
                .ToHashSet();

            var shiftWeeks = behavior == MissedSessionBehavior.Shift
                ? ComputeShiftWeeks(program, weeksWithDone, today)
                : 0;

            // Candidates: unlinked (a linked one is represented by its booking entry instead) —
            // any status shows, so a completed/skipped standalone session still appears on the
            // agenda; only PLANNED ones can be overdue.
            foreach (var row in programRows.Where(r => r.ProgramSession.ProgramSessionSessionId is null))
            {
                var ps = row.ProgramSession;

                // SHIFT only ever moves a week that has zero DONE sessions — a week that "happened"
                // (>=1 DONE, even partially) keeps its original computed date. SKIP applies no
                // shift at all.
                var extraShift = behavior == MissedSessionBehavior.Shift && !weeksWithDone.Contains(row.WeekNumber)
                    ? shiftWeeks
                    : 0;
                var finalWeek = row.WeekNumber + program.ProgramWeekOffset + extraShift;
                var displayDate = program.ProgramStartDate.AddDays((finalWeek - 1) * 7 + (ps.ProgramSessionDayOfWeek - 1));

                if (displayDate < from || displayDate > to)
                    continue;

                var isOverdue = displayDate < today && ps.ProgramSessionStatus == ProgramSessionStatus.Planned;

                entries.Add(new AgendaEntryResponse(
                    Date:              displayDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    Type:              ps.ProgramSessionType,
                    Title:             ps.ProgramSessionName,
                    Status:            EnumMappings.ProgramSessionStatusMapping.ToWire(ps.ProgramSessionStatus),
                    IsBooked:          false,
                    ProgramSessionId:  ps.ProgramSessionId,
                    SessionId:         null,
                    ParticipantCount:  null,
                    IsOverdue:         isOverdue));
            }
        }

        return entries;
    }

    // Count of fully-elapsed weeks (1..DurationWeeks) with zero DONE sessions, capped at
    // DurationWeeks. Until Lot 6.6 ships session completion, nothing can ever reach DONE, so every
    // fully-elapsed week counts as empty — this will hit the cap once enough real time has passed
    // since ProgramStartDate. Expected, not a bug: there's no way to mark a session DONE yet.
    private static int ComputeShiftWeeks(Program program, HashSet<int> weeksWithDone, DateOnly today)
    {
        var shiftWeeks = 0;
        for (var week = 1; week <= program.ProgramDurationWeeks; week++)
        {
            var weekStart = program.ProgramStartDate.AddDays((week - 1 + program.ProgramWeekOffset) * 7);
            var fullyElapsed = weekStart.AddDays(7) <= today;
            if (fullyElapsed && !weeksWithDone.Contains(week))
                shiftWeeks++;
        }

        return Math.Min(shiftWeeks, program.ProgramDurationWeeks);
    }

    // ── Shared ─────────────────────────────────────────────────────────────────

    // SessionScheduledAt is TIMESTAMPTZ — Npgsql requires DateTimeKind.Utc, and DateOnly has no
    // timezone of its own, so the range boundaries are treated as UTC calendar days. Same
    // convention as SessionSlotService.ListForCoachAsync (Lot 5.1).
    private static (DateTime FromDate, DateTime ToDateExclusive) ToDateBounds(DateOnly from, DateOnly to)
    {
        var fromDate = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var toDateExclusive = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        return (fromDate, toDateExclusive);
    }

    // Duplicated from SessionService — same tiny pure function, same reasoning: the DB is never
    // mutated by a read, so SCHEDULED → COMPLETED materialization is computed at read time.
    private static SessionStatus ComputeEffectiveStatus(Session s)
    {
        if (s.SessionStatus == SessionStatus.Cancelled) return SessionStatus.Cancelled;
        if (s.SessionStatus == SessionStatus.Completed) return SessionStatus.Completed;
        var endsAt = s.SessionScheduledAt.AddMinutes(s.SessionDurationMinutes);
        return DateTime.UtcNow > endsAt ? SessionStatus.Completed : SessionStatus.Scheduled;
    }
}
