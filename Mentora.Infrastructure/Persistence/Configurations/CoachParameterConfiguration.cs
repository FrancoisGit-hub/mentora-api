using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class CoachParameterConfiguration : IEntityTypeConfiguration<CoachParameter>
{
    public void Configure(EntityTypeBuilder<CoachParameter> builder)
    {
        builder.ToTable("COACH_PARAMETERS");

        builder.HasKey(e => e.CoachParameterId);
        builder.Property(e => e.CoachParameterId)
            .HasColumnName("COACH_PARAMETER_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.CoachParameterHourlyRateEuros)
            .HasColumnName("COACH_PARAMETER_HOURLY_RATE_EUROS")
            .IsRequired()
            .HasColumnType("numeric(8,2)")
            .HasDefaultValue(50.00m);

        builder.Property(e => e.CoachParameterCancellationDelayHours)
            .HasColumnName("COACH_PARAMETER_CANCELLATION_DELAY_HOURS")
            .IsRequired()
            .HasDefaultValue(24);

        builder.Property(e => e.CoachParameterLanguage)
            .HasColumnName("COACH_PARAMETER_LANGUAGE")
            .IsRequired()
            .HasMaxLength(2)
            .HasDefaultValue("FR");

        builder.Property(e => e.CoachParameterNotifMessages)
            .HasColumnName("COACH_PARAMETER_NOTIF_MESSAGES")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.CoachParameterNotifNewBooking)
            .HasColumnName("COACH_PARAMETER_NOTIF_NEW_BOOKING")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.CoachParameterNotifBookingCancelled)
            .HasColumnName("COACH_PARAMETER_NOTIF_BOOKING_CANCELLED")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.CoachParameterNotifMarketing)
            .HasColumnName("COACH_PARAMETER_NOTIF_MARKETING")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.CoachParameterMinBookingNoticeHours)
            .HasColumnName("COACH_PARAMETER_MIN_BOOKING_NOTICE_HOURS")
            .IsRequired()
            .HasDefaultValue(24);

        builder.Property(e => e.CoachParameterMaxBookingHorizonDays)
            .HasColumnName("COACH_PARAMETER_MAX_BOOKING_HORIZON_DAYS")
            .IsRequired()
            .HasDefaultValue(90);

        builder.Property(e => e.CoachParameterLateCancellationRefunds)
            .HasColumnName("COACH_PARAMETER_LATE_CANCELLATION_REFUNDS")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.CoachParameterIsAcceptingNewBookings)
            .HasColumnName("COACH_PARAMETER_IS_ACCEPTING_NEW_BOOKINGS")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.CoachParameterDefaultSessionDurationMinutes)
            .HasColumnName("COACH_PARAMETER_DEFAULT_SESSION_DURATION_MINUTES")
            .IsRequired()
            .HasDefaultValue(60);

        // Stored as uppercase string: SKIP / SHIFT. Same duplicated if-chain ToDb/FromDb pattern
        // as every other converted enum in this codebase (see OfferType, SessionStatus, ...).
        var missedSessionBehaviorConverter = new ValueConverter<MissedSessionBehavior, string>(
            v => MissedSessionBehaviorToDb(v),
            v => MissedSessionBehaviorFromDb(v));

        builder.Property(e => e.CoachParameterMissedSessionBehavior)
            .HasColumnName("COACH_PARAMETER_MISSED_SESSION_BEHAVIOR")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(missedSessionBehaviorConverter)
            .HasDefaultValueSql("'SKIP'");

        builder.Property(e => e.CoachParameterCreatedDate)
            .HasColumnName("COACH_PARAMETER_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.CoachParameterUpdatedDate)
            .HasColumnName("COACH_PARAMETER_UPDATED_DATE")
            .IsRequired();

        builder.Property(e => e.CoachId)
            .HasColumnName("COACH_ID")
            .IsRequired();

        builder.HasIndex(e => e.CoachId).IsUnique();

        builder.HasOne(e => e.Coach)
            .WithOne(c => c.CoachParameter)
            .HasForeignKey<CoachParameter>(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static string MissedSessionBehaviorToDb(MissedSessionBehavior v)
    {
        if (v == MissedSessionBehavior.Skip)  return "SKIP";
        if (v == MissedSessionBehavior.Shift) return "SHIFT";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown MissedSessionBehavior value.");
    }

    private static MissedSessionBehavior MissedSessionBehaviorFromDb(string v)
    {
        if (v == "SKIP")  return MissedSessionBehavior.Skip;
        if (v == "SHIFT") return MissedSessionBehavior.Shift;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown MissedSessionBehavior DB value.");
    }
}
