using Mentora.Core.DTOs.Catalog;
using Mentora.Core.DTOs.Coach;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class CoachMeService(MentoraDbContext db) : ICoachMeService
{
    public async Task<CoachMeResponseDto> GetAsync(Guid coachId, CancellationToken ct)
    {
        var now            = DateTime.UtcNow;
        var monthStart     = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var prevMonthStart = monthStart.AddMonths(-1);
        var sevenDaysEnd   = now.AddDays(7);

        // Query 1 — coach profile + billing parameters
        var coach = await db.Coaches
            .Include(c => c.CoachParameter)
            .Include(c => c.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CoachId == coachId, ct)
            ?? throw new NotFoundException("Coach not found.");

        if (coach.CoachParameter is null)
            throw new InvalidOperationException(
                $"CoachParameters missing for coach {coachId}. Seeder invariant broken.");

        // Query 2 — active members linked to this coach
        var activeMemberCoaches = await db.MemberCoaches
            .Where(mc => mc.CoachId == coachId && mc.Member.MemberIsActive)
            .Include(mc => mc.Member)
                .ThenInclude(m => m.User)
            .AsNoTracking()
            .ToListAsync(ct);

        var activeMembers = activeMemberCoaches
            .OrderBy(mc => mc.Member.MemberLastName)
            .ThenBy(mc => mc.Member.MemberFirstName)
            .Select(mc => ToMemberSummaryDto(mc))
            .ToList();

        // Query 3 — upcoming sessions in the next 7 days
        var upcomingSessions = await db.Sessions
            .Include(s => s.Member)
            .Where(s => s.SessionCoachId == coachId
                     && s.SessionStatus == SessionStatus.Scheduled
                     && s.SessionScheduledAt > now
                     && s.SessionScheduledAt <= sevenDaysEnd)
            .OrderBy(s => s.SessionScheduledAt)
            .AsNoTracking()
            .ToListAsync(ct);

        var upcomingSessionDtos = upcomingSessions
            .Select(s => ToUpcomingSessionDto(s))
            .ToList();

        // Query 4 — published offers
        var offers = await db.Products
            .Where(p => p.CoachId == coachId && p.ProductStatus == ProductStatus.Published)
            .OrderByDescending(p => p.ProductCreatedDate)
            .AsNoTracking()
            .ToListAsync(ct);

        var offerDtos = offers
            .Select(p => ToProductResponse(p))
            .ToList();

        // Query set 5 — stats (DB-side aggregation, no client-side counting)
        var appointmentsThisMonth = await db.Sessions.CountAsync(s =>
            s.SessionCoachId == coachId
            && s.SessionScheduledAt >= monthStart
            && (s.SessionStatus == SessionStatus.Scheduled || s.SessionStatus == SessionStatus.Completed), ct);

        var revenueThisMonth = await db.Orders
            .Where(o => o.CoachId == coachId && o.Status == OrderStatus.Paid && o.PaidAt >= monthStart)
            .SumAsync(o => (decimal?)o.TotalEuros, ct) ?? 0m;

        var sessionsSoldThisMonth = await db.SessionVouchers.CountAsync(v =>
            v.CoachId == coachId && v.CreatedDate >= monthStart, ct);

        var revenuePrevMonth = await db.Orders
            .Where(o => o.CoachId == coachId && o.Status == OrderStatus.Paid
                     && o.PaidAt >= prevMonthStart && o.PaidAt < monthStart)
            .SumAsync(o => (decimal?)o.TotalEuros, ct) ?? 0m;

        var sessionsSoldPrevMonth = await db.SessionVouchers.CountAsync(v =>
            v.CoachId == coachId && v.CreatedDate >= prevMonthStart && v.CreatedDate < monthStart, ct);

        var revenueTotal = await db.Orders
            .Where(o => o.CoachId == coachId && o.Status == OrderStatus.Paid)
            .SumAsync(o => (decimal?)o.TotalEuros, ct) ?? 0m;

        var stats = new CoachStatsDto(
            AppointmentsThisMonth: appointmentsThisMonth,
            RevenueThisMonth:      revenueThisMonth,
            SessionsSoldThisMonth: sessionsSoldThisMonth,
            RevenuePrevMonth:      revenuePrevMonth,
            SessionsSoldPrevMonth: sessionsSoldPrevMonth,
            ActiveMembersCount:    activeMemberCoaches.Count,
            RevenueTotal:          revenueTotal);

        return new CoachMeResponseDto(
            Profile:          ToProfileDto(coach),
            ActiveMembers:    activeMembers,
            UpcomingSessions: upcomingSessionDtos,
            Offers:           offerDtos,
            Stats:            stats);
    }

    // ── Mappers ──────────────────────────────────────────────────────────────

    private static CoachProfileDto ToProfileDto(Coach c) =>
        new(
            CoachId:                c.CoachId,
            UserId:                 c.UserId,
            FirstName:              c.CoachFirstName,
            LastName:               c.CoachLastName,
            Email:                  c.User.UserEmail,
            Phone:                  c.CoachPhone,
            CreatedDate:            c.CoachCreatedDate,
            IsActive:               c.CoachIsActive,
            HourlyRateEuros:        c.CoachParameter!.CoachParameterHourlyRateEuros,
            CancellationDelayHours: c.CoachParameter!.CoachParameterCancellationDelayHours);

    private static CoachMemberSummaryDto ToMemberSummaryDto(MemberCoach mc) =>
        new(
            MemberId:  mc.MemberId,
            FirstName: mc.Member.MemberFirstName,
            LastName:  mc.Member.MemberLastName,
            Email:     mc.Member.User.UserEmail,
            Phone:     mc.Member.MemberPhone);

    private static CoachUpcomingSessionDto ToUpcomingSessionDto(Session s) =>
        new(
            SessionId:       s.SessionId,
            MemberId:        s.SessionMemberId,
            MemberFirstName: s.Member.MemberFirstName,
            MemberLastName:  s.Member.MemberLastName,
            ScheduledAt:     s.SessionScheduledAt,
            DurationMinutes: s.SessionDurationMinutes,
            OfferType:       EnumMappings.OfferTypeMapping.ToWire(s.SessionOfferType),
            VisioUrl:        s.SessionVisioUrl);

    private static ProductResponse ToProductResponse(Product p) => new(
        p.ProductId,
        p.ProductName,
        p.ProductDescription,
        EnumMappings.OfferTypeMapping.ToWire(p.ProductOfferType),
        p.ProductOfferNature,
        p.ProductDurationMinutes,
        p.ProductPriceEuros,
        EnumMappings.SportMapping.ToWire(p.ProductSport),
        p.ProductLocation,
        p.ProductTags,
        EnumMappings.ProductStatusMapping.ToWire(p.ProductStatus),
        p.OfferProgramId,
        p.CoachId,
        p.ProductCreatedDate,
        p.ProductUpdatedDate);
}
