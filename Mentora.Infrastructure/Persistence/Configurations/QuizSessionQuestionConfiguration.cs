using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class QuizSessionQuestionConfiguration : IEntityTypeConfiguration<QuizSessionQuestion>
{
    public void Configure(EntityTypeBuilder<QuizSessionQuestion> builder)
    {
        builder.ToTable("QUIZ_SESSION_QUESTIONS");

        builder.HasKey(q => q.QuizSessionQuestionId);
        builder.Property(q => q.QuizSessionQuestionId)
            .HasColumnName("QUIZ_SESSION_QUESTION_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        // CHECK constraints (position, chosen answer, points earned, answer state) are raw SQL in
        // the Lot10_Quiz migration.
        builder.Property(q => q.QuizSessionQuestionSessionId).HasColumnName("QUIZ_SESSION_QUESTION_SESSION_ID").IsRequired();
        builder.Property(q => q.QuizSessionQuestionQuestionId).HasColumnName("QUIZ_SESSION_QUESTION_QUESTION_ID").IsRequired();
        builder.Property(q => q.QuizSessionQuestionPosition).HasColumnName("QUIZ_SESSION_QUESTION_POSITION").IsRequired();
        builder.Property(q => q.QuizSessionQuestionChosenAnswer).HasColumnName("QUIZ_SESSION_QUESTION_CHOSEN_ANSWER").HasMaxLength(1);
        builder.Property(q => q.QuizSessionQuestionIsCorrect).HasColumnName("QUIZ_SESSION_QUESTION_IS_CORRECT");
        builder.Property(q => q.QuizSessionQuestionPointsEarned).HasColumnName("QUIZ_SESSION_QUESTION_POINTS_EARNED");
        builder.Property(q => q.QuizSessionQuestionCreatedDate).HasColumnName("QUIZ_SESSION_QUESTION_CREATED_DATE").IsRequired();
        builder.Property(q => q.QuizSessionQuestionUpdatedDate).HasColumnName("QUIZ_SESSION_QUESTION_UPDATED_DATE").IsRequired();

        builder.HasIndex(q => new { q.QuizSessionQuestionSessionId, q.QuizSessionQuestionPosition })
            .IsUnique()
            .HasDatabaseName("UX_QUIZ_SESSION_QUESTIONS_SESSION_POSITION");
        builder.HasIndex(q => new { q.QuizSessionQuestionSessionId, q.QuizSessionQuestionQuestionId })
            .IsUnique()
            .HasDatabaseName("UX_QUIZ_SESSION_QUESTIONS_SESSION_QUESTION");
        builder.HasIndex(q => q.QuizSessionQuestionQuestionId).HasDatabaseName("IX_QUIZ_SESSION_QUESTIONS_QUESTION_ID");

        builder.HasOne(q => q.Session)
            .WithMany(s => s.Questions)
            .HasForeignKey(q => q.QuizSessionQuestionSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.Question)
            .WithMany()
            .HasForeignKey(q => q.QuizSessionQuestionQuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
