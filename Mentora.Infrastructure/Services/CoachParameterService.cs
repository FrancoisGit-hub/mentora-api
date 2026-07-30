using FluentValidation;
using Mentora.Core.DTOs.Lot2;
using Mentora.Core.Entities;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class CoachParameterService(
    MentoraDbContext db,
    IValidator<UpdateCoachParameterRequest> validator) : ICoachParameterService
{
    public async Task<CoachParameterDto> GetAsync(Guid coachId, CancellationToken ct)
    {
        var coach = await db.Coaches
            .Include(c => c.User)
            .Include(c => c.CoachParameter)
            .FirstOrDefaultAsync(c => c.CoachId == coachId, ct)
            ?? throw new NotFoundException($"Coach {coachId} not found.");

        if (coach.CoachParameter is null)
        {
            var param = CreateDefaultParameter(coach.CoachId);
            db.CoachParameters.Add(param);
            coach.CoachParameter = param;
            await db.SaveChangesAsync(ct);
        }

        return ToDto(coach);
    }

    public async Task<CoachParameterDto> UpdateAsync(Guid coachId, UpdateCoachParameterRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var coach = await db.Coaches
            .Include(c => c.User)
            .Include(c => c.CoachParameter)
            .FirstOrDefaultAsync(c => c.CoachId == coachId, ct)
            ?? throw new NotFoundException($"Coach {coachId} not found.");

        // Get-or-create: the startup seeder only backfills coaches that existed at that
        // startup, so a coach created since then can still be missing this row.
        if (coach.CoachParameter is null)
        {
            var param = CreateDefaultParameter(coach.CoachId);
            db.CoachParameters.Add(param);
            coach.CoachParameter = param;
        }

        // COACHES — identity fields
        coach.CoachFirstName = request.FirstName.Trim();
        coach.CoachLastName  = request.LastName.Trim();
        coach.CoachPhone     = NormalizePhone(request.Phone);

        // COACH_PARAMETERS — billing / booking / delivery / prefs
        var p = coach.CoachParameter!;
        p.CoachParameterHourlyRateEuros              = request.HourlyRateEuros;
        p.CoachParameterCancellationDelayHours        = request.CancellationDelayHours;
        p.CoachParameterMinBookingNoticeHours         = request.MinBookingNoticeHours;
        p.CoachParameterMaxBookingHorizonDays         = request.MaxBookingHorizonDays;
        p.CoachParameterLateCancellationRefunds       = request.LateCancellationRefunds;
        p.CoachParameterIsAcceptingNewBookings        = request.IsAcceptingNewBookings;
        p.CoachParameterDefaultSessionDurationMinutes = request.DefaultSessionDurationMinutes;
        p.CoachParameterPresentialAddress             = request.PresentialAddress?.Trim();
        p.CoachParameterLanguage                      = request.Language.ToUpperInvariant();
        p.CoachParameterNotifMessages                 = request.NotifMessages;
        p.CoachParameterNotifNewBooking               = request.NotifNewBooking;
        p.CoachParameterNotifBookingCancelled         = request.NotifBookingCancelled;
        p.CoachParameterNotifMarketing                = request.NotifMarketing;
        p.CoachParameterUpdatedDate                   = DateTime.UtcNow;

        // One SaveChangesAsync persists COACHES and COACH_PARAMETERS together.
        await db.SaveChangesAsync(ct);

        return ToDto(coach);
    }

    private static CoachParameter CreateDefaultParameter(Guid coachId)
    {
        var now = DateTime.UtcNow;
        return new CoachParameter
        {
            CoachId                   = coachId,
            CoachParameterCreatedDate = now,
            CoachParameterUpdatedDate = now
        };
    }

    private static string? NormalizePhone(string? phone)
    {
        var trimmed = phone?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static CoachParameterDto ToDto(Coach c)
    {
        var p = c.CoachParameter!;
        return new CoachParameterDto(
            FirstName:                     c.CoachFirstName,
            LastName:                      c.CoachLastName,
            Email:                         c.User.UserEmail,
            Phone:                         c.CoachPhone,
            HourlyRateEuros:               p.CoachParameterHourlyRateEuros,
            CancellationDelayHours:        p.CoachParameterCancellationDelayHours,
            MinBookingNoticeHours:         p.CoachParameterMinBookingNoticeHours,
            MaxBookingHorizonDays:         p.CoachParameterMaxBookingHorizonDays,
            LateCancellationRefunds:       p.CoachParameterLateCancellationRefunds,
            IsAcceptingNewBookings:        p.CoachParameterIsAcceptingNewBookings,
            DefaultSessionDurationMinutes: p.CoachParameterDefaultSessionDurationMinutes,
            PresentialAddress:             p.CoachParameterPresentialAddress,
            Language:                      p.CoachParameterLanguage,
            NotifMessages:                 p.CoachParameterNotifMessages,
            NotifNewBooking:               p.CoachParameterNotifNewBooking,
            NotifBookingCancelled:         p.CoachParameterNotifBookingCancelled,
            NotifMarketing:                p.CoachParameterNotifMarketing,
            UpdatedAt:                     p.CoachParameterUpdatedDate);
    }
}
