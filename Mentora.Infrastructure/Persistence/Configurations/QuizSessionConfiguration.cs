using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class QuizSessionConfiguration : IEntityTypeConfiguration<QuizSession>
{
    public void Configure(EntityTypeBuilder<QuizSession> builder)
    {
        builder.ToTable("QUIZ_SESSIONS");

        builder.HasKey(s => s.QuizSessionId);
        builder.Property(s => s.QuizSessionId)
            .HasColumnName("QUIZ_SESSION_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        // CHECK constraints (owner, status, question count, completion state) are raw SQL in the
        // Lot10_Quiz migration.
        builder.Property(s => s.QuizSessionCoachId).HasColumnName("QUIZ_SESSION_COACH_ID");
        builder.Property(s => s.QuizSessionDeviceId).HasColumnName("QUIZ_SESSION_DEVICE_ID").HasMaxLength(64);

        var statusConverter = new ValueConverter<QuizSessionStatus, string>(
            v => QuizSessionStatusToDb(v),
            v => QuizSessionStatusFromDb(v));

        builder.Property(s => s.QuizSessionStatus)
            .HasColumnName("QUIZ_SESSION_STATUS")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(statusConverter);

        builder.Property(s => s.QuizSessionQuestionCount).HasColumnName("QUIZ_SESSION_QUESTION_COUNT").IsRequired();
        builder.Property(s => s.QuizSessionScore).HasColumnName("QUIZ_SESSION_SCORE");
        builder.Property(s => s.QuizSessionCorrectCount).HasColumnName("QUIZ_SESSION_CORRECT_COUNT");
        builder.Property(s => s.QuizSessionExpiresDate).HasColumnName("QUIZ_SESSION_EXPIRES_DATE").IsRequired();
        builder.Property(s => s.QuizSessionCompletedDate).HasColumnName("QUIZ_SESSION_COMPLETED_DATE");
        builder.Property(s => s.QuizSessionCreatedDate).HasColumnName("QUIZ_SESSION_CREATED_DATE").IsRequired();
        builder.Property(s => s.QuizSessionUpdatedDate).HasColumnName("QUIZ_SESSION_UPDATED_DATE").IsRequired();

        builder.HasIndex(s => s.QuizSessionCoachId).HasDatabaseName("IX_QUIZ_SESSIONS_COACH_ID");
        builder.HasIndex(s => s.QuizSessionDeviceId).HasDatabaseName("IX_QUIZ_SESSIONS_DEVICE_ID");

        builder.HasOne(s => s.Coach)
            .WithMany()
            .HasForeignKey(s => s.QuizSessionCoachId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string QuizSessionStatusToDb(QuizSessionStatus v)
    {
        if (v == QuizSessionStatus.InProgress) return "IN_PROGRESS";
        if (v == QuizSessionStatus.Completed)  return "COMPLETED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown QuizSessionStatus value.");
    }

    private static QuizSessionStatus QuizSessionStatusFromDb(string v)
    {
        if (v == "IN_PROGRESS") return QuizSessionStatus.InProgress;
        if (v == "COMPLETED")   return QuizSessionStatus.Completed;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown QuizSessionStatus DB value.");
    }
}
