using System.Text.Json;
using FluentValidation;
using Mentora.Core.DTOs.ProgramTemplate;
using Mentora.Core.DTOs.ProgramTemplate.Body;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class ProgramTemplateService(
    MentoraDbContext db,
    IValidator<ProgramTemplateRequest> validator) : IProgramTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<List<ProgramTemplateHeaderResponse>> ListForCoachAsync(
        Guid coachId, string? search, string? goal, string scope, bool includeInactive, CancellationToken ct)
    {
        ValidateScope(scope);
        var goalFilter = ParseGoalFilter(goal);

        var query = db.ProgramTemplates
            .Where(t => t.ProgramTemplateCoachId == null || t.ProgramTemplateCoachId == coachId);

        if (scope == "MENTORA")
            query = query.Where(t => t.ProgramTemplateCoachId == null);
        else if (scope == "MINE")
            query = query.Where(t => t.ProgramTemplateCoachId == coachId);

        if (!includeInactive)
            query = query.Where(t => t.ProgramTemplateIsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => EF.Functions.ILike(t.ProgramTemplateName, $"%{search.Trim()}%"));

        if (goalFilter is not null)
            query = query.Where(t => t.ProgramTemplateGoal == goalFilter.Value);

        // Headers only — the body column is never selected here, keeping the list payload small.
        return await query
            .OrderBy(t => t.ProgramTemplateName)
            .Select(t => ToHeaderResponse(t))
            .ToListAsync(ct);
    }

    public async Task<ProgramTemplateResponse> GetByIdForCoachAsync(
        Guid programTemplateId, Guid coachId, CancellationToken ct)
    {
        var template = await db.ProgramTemplates
            .FirstOrDefaultAsync(t => t.ProgramTemplateId == programTemplateId
                                    && (t.ProgramTemplateCoachId == null || t.ProgramTemplateCoachId == coachId), ct)
            ?? throw new NotFoundException($"Program template {programTemplateId} not found.");

        return ToResponse(template);
    }

    public async Task<ProgramTemplateResponse> CreateAsync(
        ProgramTemplateRequest request, Guid coachId, CancellationToken ct)
    {
        await ValidateAsync(request, coachId, ct);

        var now = DateTime.UtcNow;
        var template = new ProgramTemplate
        {
            ProgramTemplateCoachId       = coachId,
            ProgramTemplateName          = request.Name.Trim(),
            ProgramTemplateDescription   = request.Description?.Trim(),
            ProgramTemplateGoal          = EnumMappings.ProgramGoalMapping.Parse(request.Goal),
            ProgramTemplateDurationWeeks = request.DurationWeeks,
            ProgramTemplateBody          = JsonSerializer.Serialize(request.Body, JsonOptions),
            ProgramTemplateIsActive      = true,
            ProgramTemplateCreatedDate   = now,
            ProgramTemplateUpdatedDate   = now,
        };

        db.ProgramTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return ToResponse(template);
    }

    public async Task<ProgramTemplateResponse> UpdateAsync(
        Guid programTemplateId, ProgramTemplateRequest request, Guid coachId, CancellationToken ct)
    {
        // Write rule: only rows owned by this coach — a Mentora template (CoachId IS NULL) or
        // another coach's row falls out of this predicate and 404s, never 403. Ownership wins
        // over body validation: an invalid body on someone else's template must still 404.
        var template = await db.ProgramTemplates
            .FirstOrDefaultAsync(t => t.ProgramTemplateId == programTemplateId && t.ProgramTemplateCoachId == coachId, ct)
            ?? throw new NotFoundException($"Program template {programTemplateId} not found.");

        await ValidateAsync(request, coachId, ct);

        template.ProgramTemplateName          = request.Name.Trim();
        template.ProgramTemplateDescription   = request.Description?.Trim();
        template.ProgramTemplateGoal          = EnumMappings.ProgramGoalMapping.Parse(request.Goal);
        template.ProgramTemplateDurationWeeks = request.DurationWeeks;
        template.ProgramTemplateBody          = JsonSerializer.Serialize(request.Body, JsonOptions);
        template.ProgramTemplateUpdatedDate   = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return ToResponse(template);
    }

    public async Task DeleteAsync(Guid programTemplateId, Guid coachId, CancellationToken ct)
    {
        var template = await db.ProgramTemplates
            .FirstOrDefaultAsync(t => t.ProgramTemplateId == programTemplateId && t.ProgramTemplateCoachId == coachId, ct)
            ?? throw new NotFoundException($"Program template {programTemplateId} not found.");

        template.ProgramTemplateIsActive    = false;
        template.ProgramTemplateUpdatedDate = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task ValidateAsync(ProgramTemplateRequest request, Guid coachId, CancellationToken ct)
    {
        var validationContext = new ValidationContext<ProgramTemplateRequest>(request);
        validationContext.RootContextData["CoachId"] = coachId;

        var result = await validator.ValidateAsync(validationContext, ct);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);
    }

    private static void ValidateScope(string scope)
    {
        if (scope != "ALL" && scope != "MENTORA" && scope != "MINE")
            throw new InvalidOperationException($"Invalid scope '{scope}'. Accepted values: ALL, MENTORA, MINE.");
    }

    private static ProgramGoal? ParseGoalFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim().ToUpperInvariant();
        if (!EnumMappings.ProgramGoalMapping.WireValues.Contains(normalized))
            throw new InvalidOperationException(
                $"Invalid goal '{value}'. Accepted values: {string.Join(", ", EnumMappings.ProgramGoalMapping.WireValues)}.");

        return EnumMappings.ProgramGoalMapping.Parse(normalized);
    }

    private static ProgramTemplateHeaderResponse ToHeaderResponse(ProgramTemplate t) => new(
        t.ProgramTemplateId,
        t.ProgramTemplateCoachId,
        t.ProgramTemplateName,
        t.ProgramTemplateDescription,
        EnumMappings.ProgramGoalMapping.ToWire(t.ProgramTemplateGoal),
        t.ProgramTemplateDurationWeeks,
        t.ProgramTemplateIsActive,
        t.ProgramTemplateCreatedDate,
        t.ProgramTemplateUpdatedDate
    );

    private static ProgramTemplateResponse ToResponse(ProgramTemplate t) => new(
        t.ProgramTemplateId,
        t.ProgramTemplateCoachId,
        t.ProgramTemplateName,
        t.ProgramTemplateDescription,
        EnumMappings.ProgramGoalMapping.ToWire(t.ProgramTemplateGoal),
        t.ProgramTemplateDurationWeeks,
        JsonSerializer.Deserialize<ProgramTemplateBody>(t.ProgramTemplateBody, JsonOptions)!,
        t.ProgramTemplateIsActive,
        t.ProgramTemplateCreatedDate,
        t.ProgramTemplateUpdatedDate
    );
}
