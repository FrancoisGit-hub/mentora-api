using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ProgramSessionConfiguration : IEntityTypeConfiguration<ProgramSession>
{
    public void Configure(EntityTypeBuilder<ProgramSession> builder)
    {
        builder.ToTable("PROGRAM_SESSIONS");

        builder.HasKey(e => e.ProgramSessionId);
        builder.Property(e => e.ProgramSessionId)
            .HasColumnName("PROGRAM_SESSION_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.ProgramSessionProgramId)
            .HasColumnName("PROGRAM_SESSION_PROGRAM_ID")
            .IsRequired();

        builder.Property(e => e.ProgramSessionBlockId)
            .HasColumnName("PROGRAM_SESSION_BLOCK_ID")
            .IsRequired();

        // Denormalized copies of PROGRAM_COACH_ID/PROGRAM_MEMBER_ID for scoped-read performance
        // (no separate FK — the real constraint lives on PROGRAMS; the service keeps these in
        // sync since rows are written once by the copy and never re-parented).
        builder.Property(e => e.ProgramSessionCoachId)
            .HasColumnName("PROGRAM_SESSION_COACH_ID")
            .IsRequired();

        builder.Property(e => e.ProgramSessionMemberId)
            .HasColumnName("PROGRAM_SESSION_MEMBER_ID")
            .IsRequired();

        // Unused until Lot 6.5
        builder.Property(e => e.ProgramSessionSessionId)
            .HasColumnName("PROGRAM_SESSION_SESSION_ID");

        builder.Property(e => e.ProgramSessionName)
            .HasColumnName("PROGRAM_SESSION_NAME")
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(e => e.ProgramSessionType)
            .HasColumnName("PROGRAM_SESSION_TYPE")
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(e => e.ProgramSessionDayOfWeek)
            .HasColumnName("PROGRAM_SESSION_DAY_OF_WEEK")
            .IsRequired();

        builder.Property(e => e.ProgramSessionPosition)
            .HasColumnName("PROGRAM_SESSION_POSITION")
            .IsRequired();

        var statusConverter = new ValueConverter<ProgramSessionStatus, string>(
            v => ProgramSessionStatusToDb(v),
            v => ProgramSessionStatusFromDb(v));

        builder.Property(e => e.ProgramSessionStatus)
            .HasColumnName("PROGRAM_SESSION_STATUS")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(statusConverter)
            .HasDefaultValueSql("'PLANNED'");

        builder.Property(e => e.ProgramSessionCompletedDate)
            .HasColumnName("PROGRAM_SESSION_COMPLETED_DATE");

        builder.Property(e => e.ProgramSessionMemberFeedback)
            .HasColumnName("PROGRAM_SESSION_MEMBER_FEEDBACK")
            .HasColumnType("text");

        builder.Property(e => e.ProgramSessionCoachNote)
            .HasColumnName("PROGRAM_SESSION_COACH_NOTE")
            .HasColumnType("text");

        // CHECK (TYPE <> 'A_DISTANCE' OR SESSION_ID IS NULL) is created via raw SQL in the
        // migration: an A_DISTANCE session never carries a booking. The converse is NOT enforced —
        // a PRESENTIEL_SOLO/PRESENTIEL_GROUPE/VISIO session legitimately has no booking yet (the
        // program is written weeks ahead of the slot being reserved). No date column — the date is
        // computed by ProgramService from PROGRAM_START_DATE, the parent MICROCYCLE's WEEK_NUMBER,
        // WEEK_OFFSET and DAY_OF_WEEK.

        builder.HasOne<Program>()
            .WithMany()
            .HasForeignKey(e => e.ProgramSessionProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ProgramBlock>()
            .WithMany()
            .HasForeignKey(e => e.ProgramSessionBlockId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unused until Lot 6.5. SetNull is correct here: if the booking disappears, losing the
        // link is the right outcome.
        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(e => e.ProgramSessionSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static string ProgramSessionStatusToDb(ProgramSessionStatus v)
    {
        if (v == ProgramSessionStatus.Planned) return "PLANNED";
        if (v == ProgramSessionStatus.Done)    return "DONE";
        if (v == ProgramSessionStatus.Skipped) return "SKIPPED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ProgramSessionStatus value.");
    }

    private static ProgramSessionStatus ProgramSessionStatusFromDb(string v)
    {
        if (v == "PLANNED") return ProgramSessionStatus.Planned;
        if (v == "DONE")    return ProgramSessionStatus.Done;
        if (v == "SKIPPED") return ProgramSessionStatus.Skipped;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ProgramSessionStatus DB value.");
    }
}
