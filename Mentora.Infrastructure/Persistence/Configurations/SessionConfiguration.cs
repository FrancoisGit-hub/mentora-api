using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("SESSIONS");

        builder.HasKey(e => e.SessionId);
        builder.Property(e => e.SessionId)
            .HasColumnName("SESSION_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.SessionVoucherId)
            .HasColumnName("SESSION_VOUCHER_ID")
            .IsRequired();

        builder.Property(e => e.SessionSlotId)
            .HasColumnName("SESSION_SLOT_ID")
            .IsRequired();

        builder.Property(e => e.SessionMemberId)
            .HasColumnName("SESSION_MEMBER_ID")
            .IsRequired();

        builder.Property(e => e.SessionCoachId)
            .HasColumnName("SESSION_COACH_ID")
            .IsRequired();

        // Weak reference — no FK; snapshot semantics (product may be archived)
        builder.Property(e => e.SessionProductId)
            .HasColumnName("SESSION_PRODUCT_ID")
            .IsRequired();

        var offerTypeConverter = new ValueConverter<OfferType, string>(
            v => OfferTypeToDb(v),
            v => OfferTypeFromDb(v));

        builder.Property(e => e.SessionOfferType)
            .HasColumnName("SESSION_OFFER_TYPE")
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion(offerTypeConverter);

        builder.Property(e => e.SessionDurationMinutes)
            .HasColumnName("SESSION_DURATION_MINUTES")
            .IsRequired();

        var sportConverter = new ValueConverter<Sport, string>(
            v => SportToDb(v),
            v => SportFromDb(v));

        builder.Property(e => e.SessionSport)
            .HasColumnName("SESSION_SPORT")
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion(sportConverter);

        builder.Property(e => e.SessionScheduledAt)
            .HasColumnName("SESSION_SCHEDULED_AT")
            .IsRequired();

        var statusConverter = new ValueConverter<SessionStatus, string>(
            v => SessionStatusToDb(v),
            v => SessionStatusFromDb(v));

        builder.Property(e => e.SessionStatus)
            .HasColumnName("SESSION_STATUS")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(statusConverter)
            .HasDefaultValueSql("'SCHEDULED'");

        builder.Property(e => e.SessionVisioUrl)
            .HasColumnName("SESSION_VISIO_URL")
            .HasColumnType("text");

        builder.Property(e => e.SessionCancellationReason)
            .HasColumnName("SESSION_CANCELLATION_REASON")
            .HasColumnType("text");

        var cancelledByConverter = new ValueConverter<CancelledBy, string>(
            v => CancelledByToDb(v),
            v => CancelledByFromDb(v));

        builder.Property(e => e.SessionCancelledBy)
            .HasColumnName("SESSION_CANCELLED_BY")
            .HasMaxLength(10)
            .HasConversion(cancelledByConverter);

        builder.Property(e => e.SessionCancelledAt)
            .HasColumnName("SESSION_CANCELLED_AT");

        builder.Property(e => e.SessionCompletedAt)
            .HasColumnName("SESSION_COMPLETED_AT");

        builder.Property(e => e.SessionCreatedDate)
            .HasColumnName("SESSION_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.SessionUpdatedDate)
            .HasColumnName("SESSION_UPDATED_DATE")
            .IsRequired();

        // Coach calendar view: sessions by coach ordered by time
        builder.HasIndex(e => new { e.SessionCoachId, e.SessionScheduledAt })
            .HasDatabaseName("IX_SESSIONS_COACH_SCHEDULED_AT");

        // Member session history: upcoming / past by status
        builder.HasIndex(e => new { e.SessionMemberId, e.SessionStatus })
            .HasDatabaseName("IX_SESSIONS_MEMBER_STATUS");

        builder.HasOne(e => e.Voucher)
            .WithMany()
            .HasForeignKey(e => e.SessionVoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Slot)
            .WithMany()
            .HasForeignKey(e => e.SessionSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.SessionMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Coach)
            .WithMany()
            .HasForeignKey(e => e.SessionCoachId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string OfferTypeToDb(OfferType v)
    {
        if (v == OfferType.Visio)            return "VISIO";
        if (v == OfferType.PresentielSolo)   return "PRESENTIEL_SOLO";
        if (v == OfferType.PresentielGroupe) return "PRESENTIEL_GROUPE";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown OfferType value.");
    }

    private static OfferType OfferTypeFromDb(string v)
    {
        if (v == "VISIO")             return OfferType.Visio;
        if (v == "PRESENTIEL_SOLO")   return OfferType.PresentielSolo;
        if (v == "PRESENTIEL_GROUPE") return OfferType.PresentielGroupe;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown OfferType DB value.");
    }

    private static string SportToDb(Sport v)
    {
        if (v == Sport.Training) return "TRAINING";
        if (v == Sport.Boxe)     return "BOXE";
        if (v == Sport.Salle)    return "SALLE";
        if (v == Sport.Course)   return "COURSE";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown Sport value.");
    }

    private static Sport SportFromDb(string v)
    {
        if (v == "TRAINING") return Sport.Training;
        if (v == "BOXE")     return Sport.Boxe;
        if (v == "SALLE")    return Sport.Salle;
        if (v == "COURSE")   return Sport.Course;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown Sport DB value.");
    }

    private static string SessionStatusToDb(SessionStatus v)
    {
        if (v == SessionStatus.Scheduled)  return "SCHEDULED";
        if (v == SessionStatus.Completed)  return "COMPLETED";
        if (v == SessionStatus.Cancelled)  return "CANCELLED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown SessionStatus value.");
    }

    private static SessionStatus SessionStatusFromDb(string v)
    {
        if (v == "SCHEDULED")  return SessionStatus.Scheduled;
        if (v == "COMPLETED")  return SessionStatus.Completed;
        if (v == "CANCELLED")  return SessionStatus.Cancelled;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown SessionStatus DB value.");
    }

    private static string CancelledByToDb(CancelledBy v)
    {
        if (v == CancelledBy.Member) return "MEMBER";
        if (v == CancelledBy.Coach)  return "COACH";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown CancelledBy value.");
    }

    private static CancelledBy CancelledByFromDb(string v)
    {
        if (v == "MEMBER") return CancelledBy.Member;
        if (v == "COACH")  return CancelledBy.Coach;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown CancelledBy DB value.");
    }
}
