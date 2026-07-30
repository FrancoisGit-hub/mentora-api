using FluentValidation;
using Mentora.Core.DTOs.Coach;
using Mentora.Core.Entities;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class CoachMemberSettingsService(
    MentoraDbContext db,
    IValidator<UpdateMemberSettingsRequest> validator) : ICoachMemberSettingsService
{
    public async Task<MemberSettingsDto> GetAsync(Guid coachId, Guid memberId, CancellationToken ct)
    {
        var link = await LoadLinkedAsync(coachId, memberId, ct);
        return ToDto(link);
    }

    public async Task<MemberSettingsDto> UpdateAsync(
        Guid coachId, Guid memberId, UpdateMemberSettingsRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var link = await LoadLinkedAsync(coachId, memberId, ct);

        link.MemberCoachPresentialAddress = request.PresentialAddress?.Trim();
        await db.SaveChangesAsync(ct);

        return ToDto(link);
    }

    // 404, never 403 — a 403 would confirm the member exists under a different coach.
    // The (coachId, memberId) match in the WHERE clause is both the tenant-scope check
    // and the lookup, in one query.
    private async Task<MemberCoach> LoadLinkedAsync(Guid coachId, Guid memberId, CancellationToken ct)
    {
        return await db.MemberCoaches
            .Include(mc => mc.Member).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(mc => mc.CoachId == coachId && mc.MemberId == memberId, ct)
            ?? throw new NotFoundException($"Member {memberId} not found.");
    }

    private static MemberSettingsDto ToDto(MemberCoach link) => new(
        MemberId:          link.MemberId,
        FirstName:         link.Member.MemberFirstName,
        LastName:          link.Member.MemberLastName,
        Email:             link.Member.User.UserEmail,
        PresentialAddress: link.MemberCoachPresentialAddress);
}
