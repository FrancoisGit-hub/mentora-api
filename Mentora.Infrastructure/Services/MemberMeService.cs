using Mentora.Core.DTOs.Member;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class MemberMeService(MentoraDbContext db) : IMemberMeService
{
    public async Task<MemberMeResponseDto> GetAsync(
        Guid memberId, int consumedSinceDays, CancellationToken ct)
    {
        if (consumedSinceDays < 0)
            throw new InvalidOperationException("consumedSinceDays must be 0 or greater.");

        // Query 1 — member + user + all linked coach groups with coach parameters
        var member = await db.Members
            .Include(m => m.User)
            .Include(m => m.MemberCoaches)
                .ThenInclude(mc => mc.Coach)
                    .ThenInclude(c => c.CoachParameter)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct)
            ?? throw new NotFoundException("Member not found.");

        // Query 2 — vouchers with order-item snapshot for the product name
        DateTime? cutoffUtc = consumedSinceDays > 0
            ? DateTime.UtcNow.AddDays(-consumedSinceDays)
            : null;

        var vouchers = await db.SessionVouchers
            .Include(v => v.OrderItem)
            .Where(v => v.MemberId == memberId
                     && (v.Status != VoucherStatus.Consumed
                         || (cutoffUtc.HasValue && v.UpdatedDate >= cutoffUtc.Value)))
            .AsNoTracking()
            .ToListAsync(ct);

        var vouchersByCoach = vouchers
            .GroupBy(v => v.CoachId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var coachGroups = member.MemberCoaches
            .OrderByDescending(mc => mc.IsPrimary)
            .ThenBy(mc => mc.StartedAt)
            .Select(mc =>
            {
                var coachVouchers = vouchersByCoach.TryGetValue(mc.CoachId, out var list)
                    ? list
                    : [];

                var sortedVouchers = coachVouchers
                    .OrderBy(v => StatusPriority(v.Status))
                    .ThenByDescending(v => v.UpdatedDate)
                    .Select(ToVoucherDto)
                    .ToList();

                return new MemberCoachGroupDto(
                    Coach:      ToCoachSummaryDto(mc),
                    IsPrimary:  mc.IsPrimary,
                    Vouchers:   sortedVouchers);
            })
            .ToList();

        return new MemberMeResponseDto(
            Profile: ToProfileDto(member),
            Coaches: coachGroups);
    }

    private static int StatusPriority(VoucherStatus status) => status switch
    {
        VoucherStatus.Reserved  => 0,
        VoucherStatus.Available => 1,
        _                       => 2
    };

    private static MemberProfileDto ToProfileDto(Member m) =>
        new(
            MemberId:       m.MemberId,
            UserId:         m.UserId,
            FirstName:      m.MemberFirstName,
            LastName:       m.MemberLastName,
            Email:          m.User.UserEmail,
            Phone:          m.MemberPhone,
            CreatedDate:    m.MemberCreatedDate,
            ActivationDate: m.MemberActivationDate,
            HasActivated:   m.MemberHasActivated,
            IsActive:       m.MemberIsActive);

    private static MemberCoachSummaryDto ToCoachSummaryDto(MemberCoach mc)
    {
        var coach = mc.Coach;
        var cp = coach.CoachParameter
            ?? throw new InvalidOperationException(
                $"CoachParameters missing for coach {coach.CoachId}. Seeder invariant broken.");

        return new(
            CoachId:                coach.CoachId,
            FirstName:              coach.CoachFirstName,
            LastName:               coach.CoachLastName,
            Phone:                  coach.CoachPhone,
            IsActive:               coach.CoachIsActive,
            CreatedDate:            coach.CoachCreatedDate,
            HourlyRateEuros:        cp.CoachParameterHourlyRateEuros,
            CancellationDelayHours: cp.CoachParameterCancellationDelayHours);
    }

    private static MemberVoucherDto ToVoucherDto(SessionVoucher v) =>
        new(
            VoucherId:         v.VoucherId,
            Status:            EnumMappings.VoucherStatusMapping.ToWire(v.Status),
            OfferType:         EnumMappings.OfferTypeMapping.ToWire(v.OfferType),
            DurationMinutes:   v.DurationMinutes,
            Sport:             EnumMappings.SportMapping.ToWire(v.Sport),
            ProductId:         v.ProductId,
            ProductName:       v.OrderItem.ProductName,
            ReservedSessionId: v.ReservedSessionId,
            CreatedDate:       v.CreatedDate,
            UpdatedDate:       v.UpdatedDate);
}
