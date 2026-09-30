using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Mentora.Core.DTOs.Quiz;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Mentora.Core.Validators.Quiz;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services.Quiz;

public class QuizBasicService(
    MentoraDbContext db,
    IOptions<QuizOptions> options,
    IValidator<QuizOwner> ownerValidator,
    IValidator<QuizSubmitRequest> submitValidator) : IQuizBasicService
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    private readonly QuizOptions _options = options.Value;

    // ── POST sessions ──────────────────────────────────────────────────────────

    public async Task<QuizSessionResponse> CreateSessionAsync(QuizOwner owner, CancellationToken ct)
    {
        await ownerValidator.ValidateAndThrowAsync(owner, ct);

        // Uniform random draw in SQL (ORDER BY random() LIMIT n): the question bank is never
        // loaded in memory. One query, projected to what the response needs.
        var drawn = await db.QuizQuestions
            .AsNoTracking()
            .Where(q => q.QuizQuestionIsActive)
            .OrderBy(_ => EF.Functions.Random())
            .Take(_options.QuestionCount)
            .Select(q => new
            {
                q.QuizQuestionId,
                q.QuizQuestionText,
                q.QuizQuestionAnswerA,
                q.QuizQuestionAnswerB,
                q.QuizQuestionAnswerC,
                q.QuizQuestionAnswerD,
                q.QuizQuestionCorrectAnswer,
                q.QuizQuestionExplanation,
                q.QuizQuestionPoints,
                q.QuizQuestionDifficulty,
                q.QuizQuestionSubject,
                q.QuizQuestionChapter,
            })
            .ToListAsync(ct);

        if (drawn.Count < _options.QuestionCount)
            throw new ConflictException(
                "Not enough active quiz questions.",
                new { reason = "NOT_ENOUGH_QUESTIONS" });

        var now = DateTime.UtcNow;
        var session = new QuizSession
        {
            QuizSessionId            = Guid.NewGuid(),
            QuizSessionCoachId       = owner.CoachId,
            QuizSessionDeviceId      = owner.IsCoachMode ? null : owner.DeviceId,
            QuizSessionStatus        = QuizSessionStatus.InProgress,
            QuizSessionQuestionCount = drawn.Count,
            QuizSessionExpiresDate   = now.AddMinutes(_options.SessionLifetimeMinutes),
            QuizSessionCreatedDate   = now,
            QuizSessionUpdatedDate   = now,
        };

        for (var i = 0; i < drawn.Count; i++)
        {
            session.Questions.Add(new QuizSessionQuestion
            {
                QuizSessionQuestionId         = Guid.NewGuid(),
                QuizSessionQuestionSessionId  = session.QuizSessionId,
                QuizSessionQuestionQuestionId = drawn[i].QuizQuestionId,
                QuizSessionQuestionPosition   = i + 1,
                QuizSessionQuestionCreatedDate = now,
                QuizSessionQuestionUpdatedDate = now,
            });
        }

        db.QuizSessions.Add(session);
        await db.SaveChangesAsync(ct);

        var questions = drawn
            .Select((q, i) => new QuizSessionQuestionDto(
                i + 1,
                q.QuizQuestionId,
                q.QuizQuestionText,
                [
                    new QuizAnswerOptionDto("A", q.QuizQuestionAnswerA),
                    new QuizAnswerOptionDto("B", q.QuizQuestionAnswerB),
                    new QuizAnswerOptionDto("C", q.QuizQuestionAnswerC),
                    new QuizAnswerOptionDto("D", q.QuizQuestionAnswerD),
                ],
                q.QuizQuestionCorrectAnswer,
                q.QuizQuestionExplanation,
                q.QuizQuestionPoints,
                q.QuizQuestionDifficulty,
                q.QuizQuestionSubject,
                q.QuizQuestionChapter))
            .ToList();

        return new QuizSessionResponse(session.QuizSessionId, session.QuizSessionExpiresDate,
            session.QuizSessionQuestionCount, questions);
    }

    // ── POST sessions/{id}/answers ─────────────────────────────────────────────

    public async Task<QuizSubmitResponse> SubmitAsync(
        QuizOwner owner, Guid sessionId, QuizSubmitRequest request, CancellationToken ct)
    {
        await ownerValidator.ValidateAndThrowAsync(owner, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Row lock: a concurrent submission of the same session waits here, then sees COMPLETED.
        // Ownership is part of the locked statement so a foreign session is neither locked nor revealed.
        // ToListAsync (not FirstOrDefault) keeps the raw statement uncomposed, FOR UPDATE at top level.
        List<QuizSession> locked;
        if (owner.IsCoachMode)
        {
            var coachId = owner.CoachId!.Value;
            locked = await db.QuizSessions
                .FromSqlInterpolated($"""
                    SELECT * FROM "QUIZ_SESSIONS"
                    WHERE "QUIZ_SESSION_ID" = {sessionId} AND "QUIZ_SESSION_COACH_ID" = {coachId}
                    FOR UPDATE
                    """)
                .ToListAsync(ct);
        }
        else
        {
            var deviceId = owner.DeviceId!;
            locked = await db.QuizSessions
                .FromSqlInterpolated($"""
                    SELECT * FROM "QUIZ_SESSIONS"
                    WHERE "QUIZ_SESSION_ID" = {sessionId} AND "QUIZ_SESSION_DEVICE_ID" = {deviceId}
                      AND "QUIZ_SESSION_COACH_ID" IS NULL
                    FOR UPDATE
                    """)
                .ToListAsync(ct);
        }

        var session = locked.SingleOrDefault()
            ?? throw new NotFoundException($"Quiz session {sessionId} not found.");

        // Structure of the body (422) only once the session is known to belong to the caller.
        var validationContext = new ValidationContext<QuizSubmitRequest>(request);
        validationContext.RootContextData[QuizRules.QuestionCountKey] = session.QuizSessionQuestionCount;
        var validation = await submitValidator.ValidateAsync(validationContext, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        if (session.QuizSessionStatus == QuizSessionStatus.Completed)
            throw new ConflictException("Quiz session already submitted.", new { reason = "ALREADY_SUBMITTED" });

        var now = DateTime.UtcNow;
        if (now > session.QuizSessionExpiresDate)
            throw new ConflictException("Quiz session expired.", new { reason = "EXPIRED" });

        var sessionQuestions = await db.QuizSessionQuestions
            .Include(q => q.Question)
            .Where(q => q.QuizSessionQuestionSessionId == sessionId)
            .OrderBy(q => q.QuizSessionQuestionPosition)
            .ToListAsync(ct);

        var answers = request.Answers!.ToDictionary(a => a.QuestionId, a => a.ChosenAnswer!);
        if (!answers.Keys.ToHashSet().SetEquals(sessionQuestions.Select(q => q.QuizSessionQuestionQuestionId)))
            throw new ValidationException([
                new ValidationFailure("Answers", "Answers must cover exactly the questions drawn in the session.")
            ]);

        // Scoring is done here only; nothing the client sends is trusted beyond the chosen letter.
        var results = new List<QuizResultItemDto>(sessionQuestions.Count);
        var score = 0;
        var correctCount = 0;
        foreach (var sq in sessionQuestions)
        {
            var chosen    = answers[sq.QuizSessionQuestionQuestionId];
            var isCorrect = chosen == sq.Question.QuizQuestionCorrectAnswer;
            var points    = isCorrect ? sq.Question.QuizQuestionPoints : 0;

            sq.QuizSessionQuestionChosenAnswer = chosen;
            sq.QuizSessionQuestionIsCorrect    = isCorrect;
            sq.QuizSessionQuestionPointsEarned = points;
            sq.QuizSessionQuestionUpdatedDate  = now;

            score += points;
            if (isCorrect) correctCount++;

            results.Add(new QuizResultItemDto(sq.QuizSessionQuestionPosition, sq.QuizSessionQuestionQuestionId,
                chosen, sq.Question.QuizQuestionCorrectAnswer, isCorrect, points));
        }

        session.QuizSessionStatus        = QuizSessionStatus.Completed;
        session.QuizSessionScore         = score;
        session.QuizSessionCorrectCount  = correctCount;
        session.QuizSessionCompletedDate = now;
        session.QuizSessionUpdatedDate   = now;

        await db.SaveChangesAsync(ct);

        var totalPoints = await OwnedCompleted(owner).SumAsync(s => s.QuizSessionScore ?? 0, ct);

        await transaction.CommitAsync(ct);

        return new QuizSubmitResponse(session.QuizSessionId, score, correctCount,
            session.QuizSessionQuestionCount, now, totalPoints, results);
    }

    // ── GET me ─────────────────────────────────────────────────────────────────

    public async Task<QuizHistoryResponse> GetHistoryAsync(
        QuizOwner owner, int limit, string? cursor, CancellationToken ct)
    {
        await ownerValidator.ValidateAndThrowAsync(owner, ct);

        if (limit is < 1 or > MaxLimit)
            throw new InvalidOperationException($"limit must be between 1 and {MaxLimit} (got {limit}).");

        var cursorPayload = cursor is null ? null : DecodeCursor(cursor);

        // Totals are computed at read time (no stored counter), in one aggregate query.
        var totals = await OwnedCompleted(owner)
            .GroupBy(_ => 1)
            .Select(g => new { Points = g.Sum(s => s.QuizSessionScore ?? 0), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        var page = OwnedCompleted(owner);
        if (cursorPayload is not null)
        {
            var cursorDate = cursorPayload.CompletedAt;
            var cursorId   = cursorPayload.SessionId;
            page = page.Where(s => s.QuizSessionCompletedDate < cursorDate
                                || (s.QuizSessionCompletedDate == cursorDate && s.QuizSessionId.CompareTo(cursorId) < 0));
        }

        // limit + 1 tells whether a next page exists.
        var rows = await page
            .OrderByDescending(s => s.QuizSessionCompletedDate)
            .ThenByDescending(s => s.QuizSessionId)
            .Take(limit + 1)
            .Select(s => new
            {
                s.QuizSessionId,
                CompletedAt = s.QuizSessionCompletedDate!.Value,
                Score = s.QuizSessionScore!.Value,
                CorrectCount = s.QuizSessionCorrectCount!.Value,
                s.QuizSessionQuestionCount,
            })
            .ToListAsync(ct);

        var hasNext  = rows.Count > limit;
        var pageRows = hasNext ? rows.Take(limit).ToList() : rows;

        var items = pageRows
            .Select(r => new QuizHistoryItemDto(r.QuizSessionId, r.CompletedAt, r.Score, r.CorrectCount,
                r.QuizSessionQuestionCount))
            .ToList();

        string? nextCursor = null;
        if (hasNext)
        {
            var last = pageRows[^1];
            nextCursor = EncodeCursor(last.CompletedAt, last.QuizSessionId);
        }

        return new QuizHistoryResponse(totals?.Points ?? 0, totals?.Count ?? 0, items, nextCursor);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Completed sessions of the owner: the coach's, or the device's still-unclaimed ones.</summary>
    private IQueryable<QuizSession> OwnedCompleted(QuizOwner owner)
    {
        var query = db.QuizSessions.AsNoTracking()
            .Where(s => s.QuizSessionStatus == QuizSessionStatus.Completed);

        if (owner.IsCoachMode)
        {
            var coachId = owner.CoachId!.Value;
            return query.Where(s => s.QuizSessionCoachId == coachId);
        }

        var deviceId = owner.DeviceId!;
        return query.Where(s => s.QuizSessionDeviceId == deviceId && s.QuizSessionCoachId == null);
    }

    private static string EncodeCursor(DateTime completedAt, Guid sessionId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new CursorPayload(completedAt, sessionId))));

    private static CursorPayload DecodeCursor(string cursor)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var payload = JsonSerializer.Deserialize<CursorPayload>(json);
            if (payload is null || payload.SessionId == Guid.Empty)
                throw new InvalidOperationException("Invalid cursor.");
            // Npgsql only accepts UTC timestamps.
            return payload with { CompletedAt = DateTime.SpecifyKind(payload.CompletedAt, DateTimeKind.Utc) };
        }
        catch (FormatException)   { throw new InvalidOperationException("Invalid cursor format."); }
        catch (JsonException)     { throw new InvalidOperationException("Invalid cursor format."); }
        catch (ArgumentException) { throw new InvalidOperationException("Invalid cursor format."); }
    }

    private sealed record CursorPayload(DateTime CompletedAt, Guid SessionId);
}
