using FluentValidation;
using Mentora.Core.DTOs.Member;
using Mentora.Core.Entities;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class MemberParameterService(
    MentoraDbContext db,
    IValidator<UpdateMemberParameterRequest> validator) : IMemberParameterService
{
    public async Task<MemberParameterDto> GetAsync(Guid memberId, CancellationToken ct)
    {
        var member = await db.Members
            .Include(m => m.User)
            .Include(m => m.MemberParameter)
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct)
            ?? throw new NotFoundException($"Member {memberId} not found.");

        if (member.MemberParameter is null)
        {
            var param = CreateDefaultParameter(member.MemberId);
            db.MemberParameters.Add(param);
            member.MemberParameter = param;
            await db.SaveChangesAsync(ct);
        }

        return ToDto(member);
    }

    public async Task<MemberParameterDto> UpdateAsync(Guid memberId, UpdateMemberParameterRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var member = await db.Members
            .Include(m => m.User)
            .Include(m => m.MemberParameter)
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct)
            ?? throw new NotFoundException($"Member {memberId} not found.");

        // Get-or-create: members are provisioned by a manual SQL script with no code path,
        // so a missing MEMBER_PARAMETERS row is expected, not an error.
        if (member.MemberParameter is null)
        {
            var param = CreateDefaultParameter(member.MemberId);
            db.MemberParameters.Add(param);
            member.MemberParameter = param;
        }

        // MEMBERS — physical / contact fields (FirstName, LastName, Email are NOT writable here)
        member.MemberPhone     = NormalizePhone(request.Phone);
        member.MemberGender    = request.Gender;
        member.MemberHeightCm  = request.HeightCm;
        member.MemberBirthDate = request.BirthDate;

        // MEMBER_PARAMETERS — prefs
        var p = member.MemberParameter!;
        p.MemberParameterLanguage                  = request.Language.ToUpperInvariant();
        p.MemberParameterNotifMessages              = request.NotifMessages;
        p.MemberParameterNotifSessionReminders      = request.NotifSessionReminders;
        p.MemberParameterNotifMarketing             = request.NotifMarketing;
        p.MemberParameterSessionReminderHoursBefore = request.SessionReminderHoursBefore;
        p.MemberParameterUpdatedDate                = DateTime.UtcNow;

        // One SaveChangesAsync persists MEMBERS and MEMBER_PARAMETERS together.
        await db.SaveChangesAsync(ct);

        return ToDto(member);
    }

    private static MemberParameter CreateDefaultParameter(Guid memberId)
    {
        var now = DateTime.UtcNow;
        return new MemberParameter
        {
            MemberId                   = memberId,
            MemberParameterCreatedDate = now,
            MemberParameterUpdatedDate = now
        };
    }

    private static string? NormalizePhone(string? phone)
    {
        var trimmed = phone?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static MemberParameterDto ToDto(Member m)
    {
        var p = m.MemberParameter!;
        return new MemberParameterDto(
            FirstName:                  m.MemberFirstName,
            LastName:                   m.MemberLastName,
            Email:                      m.User.UserEmail,
            Phone:                      m.MemberPhone,
            Gender:                     m.MemberGender,
            HeightCm:                   m.MemberHeightCm,
            BirthDate:                  m.MemberBirthDate,
            Language:                   p.MemberParameterLanguage,
            NotifMessages:              p.MemberParameterNotifMessages,
            NotifSessionReminders:      p.MemberParameterNotifSessionReminders,
            NotifMarketing:             p.MemberParameterNotifMarketing,
            SessionReminderHoursBefore: p.MemberParameterSessionReminderHoursBefore,
            UpdatedAt:                  p.MemberParameterUpdatedDate);
    }
}
