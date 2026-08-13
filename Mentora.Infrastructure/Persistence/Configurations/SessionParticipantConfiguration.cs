using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class SessionParticipantConfiguration : IEntityTypeConfiguration<SessionParticipant>
{
    public void Configure(EntityTypeBuilder<SessionParticipant> builder)
    {
        builder.ToTable("SESSION_PARTICIPANTS");

        builder.HasKey(e => e.SessionParticipantId);
        builder.Property(e => e.SessionParticipantId)
            .HasColumnName("SESSION_PARTICIPANT_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.SessionParticipantSessionId)
            .HasColumnName("SESSION_PARTICIPANT_SESSION_ID")
            .IsRequired();

        builder.Property(e => e.SessionParticipantMemberId)
            .HasColumnName("SESSION_PARTICIPANT_MEMBER_ID")
            .IsRequired();

        builder.Property(e => e.SessionParticipantVoucherId)
            .HasColumnName("SESSION_PARTICIPANT_VOUCHER_ID");

        var statusConverter = new ValueConverter<SessionParticipantStatus, string>(
            v => StatusToDb(v),
            v => StatusFromDb(v));

        builder.Property(e => e.SessionParticipantStatus)
            .HasColumnName("SESSION_PARTICIPANT_STATUS")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(statusConverter)
            .HasDefaultValueSql("'REGISTERED'");

        builder.Property(e => e.SessionParticipantCreatedDate)
            .HasColumnName("SESSION_PARTICIPANT_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.SessionParticipantUpdatedDate)
            .HasColumnName("SESSION_PARTICIPANT_UPDATED_DATE")
            .IsRequired();

        // Partial unique index (one active registration per session/member) is created via raw
        // SQL in the migration — same treatment as the other partial unique indexes in this
        // codebase (EXERCISES, PROGRAM_TEMPLATES, PROGRAMS).

        builder.HasOne(e => e.Session)
            .WithMany()
            .HasForeignKey(e => e.SessionParticipantSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.SessionParticipantMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Voucher)
            .WithMany()
            .HasForeignKey(e => e.SessionParticipantVoucherId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string StatusToDb(SessionParticipantStatus v)
    {
        if (v == SessionParticipantStatus.Registered) return "REGISTERED";
        if (v == SessionParticipantStatus.Attended)    return "ATTENDED";
        if (v == SessionParticipantStatus.NoShow)      return "NO_SHOW";
        if (v == SessionParticipantStatus.Cancelled)   return "CANCELLED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown SessionParticipantStatus value.");
    }

    private static SessionParticipantStatus StatusFromDb(string v)
    {
        if (v == "REGISTERED") return SessionParticipantStatus.Registered;
        if (v == "ATTENDED")   return SessionParticipantStatus.Attended;
        if (v == "NO_SHOW")    return SessionParticipantStatus.NoShow;
        if (v == "CANCELLED")  return SessionParticipantStatus.Cancelled;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown SessionParticipantStatus DB value.");
    }
}
