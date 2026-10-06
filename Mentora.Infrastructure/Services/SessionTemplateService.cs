using System.Text;
using System.Text.Json;
using FluentValidation;
using Mentora.Core.DTOs.ProgramTemplate.Body;
using Mentora.Core.DTOs.SessionTemplate;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Text;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

/// <remarks>
/// Visibility: the caller's own templates plus the active platform templates (COACH_ID IS NULL).
/// Writes (PUT, DELETE) only ever touch the caller's own rows, so a platform, another coach's or an
/// unknown template is a 404 (never 403). Platform templates are never written through the API: they come
/// from the 11.6 seeder, and a platform template may only reference Mentora catalog exercises. Duplicating a
/// template therefore does not re-validate its exercises (platform content references the catalog only,
/// own content was validated when written).
/// </remarks>
public class SessionTemplateService(
    MentoraDbContext db,
    IValidator<SessionTemplateRequest> validator) : ISessionTemplateService
{
    private const int MaxLimit = 100;
    private const int SearchMinLength = 2;
    private const int SearchMaxLength = 60;
    private const string CopySuffix = " (copie)";
    private const int NameMaxLength = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ── List ───────────────────────────────────────────────────────────────────

    public async Task<SessionTemplateListResponse> ListAsync(
        Guid coachId, string? scope, string? nature, string? sessionType, string? search,
        int limit, string? cursor, CancellationToken ct)
    {
        if (limit is < 1 or > MaxLimit)
            throw new InvalidOperationException($"limit must be between 1 and {MaxLimit} (got {limit}).");

        var scopeValue = ParseScope(scope);
        var natureFilter = ParseFilter(nature, "nature", ProgramSessionNatures.All);
        var typeFilter = ParseFilter(sessionType, "sessionType", ProgramSessionTypes.All);

        string? term = null;
        if (search is not null)
        {
            term = search.Trim();
            if (term.Length is < SearchMinLength or > SearchMaxLength)
                throw new InvalidOperationException(
                    $"search must be between {SearchMinLength} and {SearchMaxLength} characters (got {term.Length}).");
        }

        var cursorPayload = cursor is null ? null : DecodeCursor(cursor);

        // Accent- and case-insensitive key, computed in SQL (same translate() as the exercise search):
        // used for the name search, the order and the cursor, so the three always agree.
        var keyed = from t in db.SessionTemplates.AsNoTracking()
                    select new Row
                    {
                        Template = t,
                        SortKey = MentoraDbFunctions.Translate(t.SessionTemplateName, AccentFolding.Accented, AccentFolding.Plain).ToLower(),
                    };

        var query = scopeValue switch
        {
            "mine"     => keyed.Where(r => r.Template.SessionTemplateCoachId == coachId),
            "platform" => keyed.Where(r => r.Template.SessionTemplateCoachId == null && r.Template.SessionTemplateIsActive),
            _          => keyed.Where(r => r.Template.SessionTemplateCoachId == coachId
                                        || (r.Template.SessionTemplateCoachId == null && r.Template.SessionTemplateIsActive)),
        };

        if (natureFilter is not null)
            query = query.Where(r => r.Template.SessionTemplateNature == natureFilter);

        if (typeFilter is not null)
            query = query.Where(r => r.Template.SessionTemplateSessionType == typeFilter);

        if (term is not null)
        {
            var escaped = ExerciseSearchEscape(term);
            var foldedTerm = AccentFolding.Fold(escaped);
            query = query.Where(r => EF.Functions.Like(r.SortKey, "%" + foldedTerm + "%", "\\"));
        }

        if (cursorPayload is not null)
        {
            var cursorKey = cursorPayload.SortKey;
            var cursorId = cursorPayload.Id;
            query = query.Where(r => r.SortKey.CompareTo(cursorKey) > 0
                                  || (r.SortKey == cursorKey && r.Template.SessionTemplateId.CompareTo(cursorId) > 0));
        }

        // ONE SQL query for the page: limit + 1 rows tell whether a next page exists.
        var rows = await query
            .OrderBy(r => r.SortKey)
            .ThenBy(r => r.Template.SessionTemplateId)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasNext = rows.Count > limit;
        var pageRows = hasNext ? rows.Take(limit).ToList() : rows;

        var items = pageRows.Select(r => ToItem(r.Template)).ToList();
        var nextCursor = hasNext ? EncodeCursor(pageRows[^1].SortKey, pageRows[^1].Template.SessionTemplateId) : null;

        return new SessionTemplateListResponse(items, nextCursor);
    }

    // ── Read ───────────────────────────────────────────────────────────────────

    public async Task<SessionTemplateResponse> GetByIdAsync(Guid id, Guid coachId, CancellationToken ct)
    {
        var template = await FindVisibleAsync(id, coachId, ct);
        return ToResponse(template);
    }

    // ── Write ──────────────────────────────────────────────────────────────────

    public async Task<SessionTemplateResponse> CreateAsync(
        SessionTemplateRequest request, Guid coachId, CancellationToken ct)
    {
        await ValidateAsync(request, coachId, ct);

        var now = DateTime.UtcNow;
        var template = new SessionTemplate
        {
            SessionTemplateCoachId     = coachId,
            SessionTemplateSourceCode  = null,
            SessionTemplateName        = request.Name.Trim(),
            SessionTemplateDescription = NormalizeDescription(request.Description),
            SessionTemplateNature      = request.Nature,
            SessionTemplateSessionType = request.SessionType,
            SessionTemplateContent     = JsonSerializer.Serialize(request.Circuits, JsonOptions),
            SessionTemplateIsActive    = true,
            SessionTemplateCreatedDate = now,
            SessionTemplateUpdatedDate = now,
        };

        db.SessionTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return ToResponse(template);
    }

    public async Task<SessionTemplateResponse> UpdateAsync(
        Guid id, SessionTemplateRequest request, Guid coachId, CancellationToken ct)
    {
        // Ownership wins over body validation: a platform, another coach's or an unknown template
        // is a 404 even when the body is invalid.
        var template = await FindOwnedAsync(id, coachId, ct);

        await ValidateAsync(request, coachId, ct);

        template.SessionTemplateName        = request.Name.Trim();
        template.SessionTemplateDescription = NormalizeDescription(request.Description);
        template.SessionTemplateNature      = request.Nature;
        template.SessionTemplateSessionType = request.SessionType;
        template.SessionTemplateContent     = JsonSerializer.Serialize(request.Circuits, JsonOptions);
        template.SessionTemplateUpdatedDate = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return ToResponse(template);
    }

    public async Task DeleteAsync(Guid id, Guid coachId, CancellationToken ct)
    {
        // Physical delete: nothing references a template (insertion into a program is a copy, 11.S).
        var template = await FindOwnedAsync(id, coachId, ct);

        db.SessionTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
    }

    public async Task<SessionTemplateResponse> DuplicateAsync(Guid id, Guid coachId, CancellationToken ct)
    {
        var source = await FindVisibleAsync(id, coachId, ct);

        var now = DateTime.UtcNow;
        var copy = new SessionTemplate
        {
            SessionTemplateCoachId     = coachId,
            SessionTemplateSourceCode  = null,
            SessionTemplateName        = CopyName(source.SessionTemplateName),
            SessionTemplateDescription = source.SessionTemplateDescription,
            SessionTemplateNature      = source.SessionTemplateNature,
            SessionTemplateSessionType = source.SessionTemplateSessionType,
            SessionTemplateContent     = source.SessionTemplateContent,
            SessionTemplateIsActive    = true,
            SessionTemplateCreatedDate = now,
            SessionTemplateUpdatedDate = now,
        };

        db.SessionTemplates.Add(copy);
        await db.SaveChangesAsync(ct);

        return ToResponse(copy);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<SessionTemplate> FindVisibleAsync(Guid id, Guid coachId, CancellationToken ct) =>
        await db.SessionTemplates
            .FirstOrDefaultAsync(t => t.SessionTemplateId == id
                                   && (t.SessionTemplateCoachId == coachId
                                       || (t.SessionTemplateCoachId == null && t.SessionTemplateIsActive)), ct)
        ?? throw new NotFoundException($"Session template {id} not found.");

    private async Task<SessionTemplate> FindOwnedAsync(Guid id, Guid coachId, CancellationToken ct) =>
        await db.SessionTemplates
            .FirstOrDefaultAsync(t => t.SessionTemplateId == id && t.SessionTemplateCoachId == coachId, ct)
        ?? throw new NotFoundException($"Session template {id} not found.");

    private async Task ValidateAsync(SessionTemplateRequest request, Guid coachId, CancellationToken ct)
    {
        var context = new ValidationContext<SessionTemplateRequest>(request);
        context.RootContextData["CoachId"] = coachId;

        var result = await validator.ValidateAsync(context, ct);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Original name + " (copie)", the original part truncated so the result stays within 100 characters.</summary>
    private static string CopyName(string name)
    {
        var maxBase = NameMaxLength - CopySuffix.Length;
        if (name.Length > maxBase)
        {
            var cut = maxBase;
            // Never split a surrogate pair.
            if (char.IsHighSurrogate(name[cut - 1])) cut--;
            name = name[..cut].TrimEnd();
        }

        return name + CopySuffix;
    }

    private static string ParseScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope)) return "all";

        var normalized = scope.Trim().ToLowerInvariant();
        if (normalized is not ("mine" or "platform" or "all"))
            throw new InvalidOperationException($"Invalid scope '{scope}'. Accepted values: mine, platform, all.");

        return normalized;
    }

    private static string? ParseFilter(string? value, string name, string[] accepted)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim().ToUpperInvariant();
        if (!accepted.Contains(normalized))
            throw new InvalidOperationException($"Invalid {name} '{value}'. Accepted values: {string.Join(", ", accepted)}.");

        return normalized;
    }

    // LIKE escaping of the search term (same rule as the exercise search).
    private static string ExerciseSearchEscape(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static List<ProgramTemplateCircuit> ReadCircuits(string content) =>
        JsonSerializer.Deserialize<List<ProgramTemplateCircuit>>(content, JsonOptions) ?? [];

    private static (int Circuits, int Exercises) Count(List<ProgramTemplateCircuit> circuits) =>
        (circuits.Count, circuits.Sum(c => c.Exercises.Count));

    private static SessionTemplateItemResponse ToItem(SessionTemplate t)
    {
        var (circuitCount, exerciseCount) = Count(ReadCircuits(t.SessionTemplateContent));
        return new SessionTemplateItemResponse(
            t.SessionTemplateId,
            t.SessionTemplateName,
            t.SessionTemplateDescription,
            t.SessionTemplateNature,
            t.SessionTemplateSessionType,
            t.SessionTemplateCoachId is null,
            t.SessionTemplateSourceCode,
            circuitCount,
            exerciseCount,
            t.SessionTemplateCreatedDate,
            t.SessionTemplateUpdatedDate);
    }

    private static SessionTemplateResponse ToResponse(SessionTemplate t)
    {
        var circuits = ReadCircuits(t.SessionTemplateContent);
        var (circuitCount, exerciseCount) = Count(circuits);

        // Ready-to-insert program tree session node: no id, no sessionId. dayOfWeek and position are
        // placeholders (the tree validator requires 1..7 and a contiguous position from 1); the app sets
        // them when it places the node in a program.
        var node = new ProgramTemplateSession
        {
            Name      = t.SessionTemplateName,
            Type      = t.SessionTemplateSessionType,
            Nature    = t.SessionTemplateNature,
            DayOfWeek = 1,
            Position  = 1,
            Circuits  = circuits,
        };

        return new SessionTemplateResponse(
            t.SessionTemplateId,
            t.SessionTemplateName,
            t.SessionTemplateDescription,
            t.SessionTemplateNature,
            t.SessionTemplateSessionType,
            t.SessionTemplateCoachId is null,
            t.SessionTemplateSourceCode,
            circuitCount,
            exerciseCount,
            t.SessionTemplateCreatedDate,
            t.SessionTemplateUpdatedDate,
            circuits,
            node);
    }

    // ── Cursor (base64 JSON of the position in the (folded name, id) order) ─────

    private sealed class Row
    {
        public SessionTemplate Template { get; init; } = null!;
        public string SortKey { get; init; } = null!;
    }

    private sealed record CursorPayload(string SortKey, Guid Id);

    private static string EncodeCursor(string sortKey, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new CursorPayload(sortKey, id))));

    private static CursorPayload DecodeCursor(string cursor)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var payload = JsonSerializer.Deserialize<CursorPayload>(json);
            if (payload is null || payload.SortKey is null)
                throw new InvalidOperationException("Invalid cursor.");
            return payload;
        }
        catch (FormatException)   { throw new InvalidOperationException("Invalid cursor format."); }
        catch (JsonException)     { throw new InvalidOperationException("Invalid cursor format."); }
        catch (ArgumentException) { throw new InvalidOperationException("Invalid cursor format."); }
    }
}
