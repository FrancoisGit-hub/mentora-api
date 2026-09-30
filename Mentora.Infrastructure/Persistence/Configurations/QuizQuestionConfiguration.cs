using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable("QUIZ_QUESTIONS");

        builder.HasKey(q => q.QuizQuestionId);
        builder.Property(q => q.QuizQuestionId)
            .HasColumnName("QUIZ_QUESTION_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        // CHECK constraints (correct answer, points) are raw SQL in the Lot10_Quiz migration.
        builder.Property(q => q.QuizQuestionSourceCode).HasColumnName("QUIZ_QUESTION_SOURCE_CODE").IsRequired().HasMaxLength(20);
        builder.HasIndex(q => q.QuizQuestionSourceCode).IsUnique().HasDatabaseName("UX_QUIZ_QUESTIONS_SOURCE_CODE");

        builder.Property(q => q.QuizQuestionSubjectCode).HasColumnName("QUIZ_QUESTION_SUBJECT_CODE").HasMaxLength(20);
        builder.Property(q => q.QuizQuestionSubject).HasColumnName("QUIZ_QUESTION_SUBJECT").HasMaxLength(200);
        builder.Property(q => q.QuizQuestionChapterCode).HasColumnName("QUIZ_QUESTION_CHAPTER_CODE").HasMaxLength(20);
        builder.Property(q => q.QuizQuestionChapter).HasColumnName("QUIZ_QUESTION_CHAPTER").HasMaxLength(200);
        builder.Property(q => q.QuizQuestionCardCode).HasColumnName("QUIZ_QUESTION_CARD_CODE").HasMaxLength(20);
        builder.Property(q => q.QuizQuestionCardTitle).HasColumnName("QUIZ_QUESTION_CARD_TITLE").HasMaxLength(300);

        builder.Property(q => q.QuizQuestionText).HasColumnName("QUIZ_QUESTION_TEXT").IsRequired().HasColumnType("text");
        builder.Property(q => q.QuizQuestionAnswerA).HasColumnName("QUIZ_QUESTION_ANSWER_A").IsRequired().HasColumnType("text");
        builder.Property(q => q.QuizQuestionAnswerB).HasColumnName("QUIZ_QUESTION_ANSWER_B").IsRequired().HasColumnType("text");
        builder.Property(q => q.QuizQuestionAnswerC).HasColumnName("QUIZ_QUESTION_ANSWER_C").IsRequired().HasColumnType("text");
        builder.Property(q => q.QuizQuestionAnswerD).HasColumnName("QUIZ_QUESTION_ANSWER_D").IsRequired().HasColumnType("text");
        builder.Property(q => q.QuizQuestionCorrectAnswer).HasColumnName("QUIZ_QUESTION_CORRECT_ANSWER").IsRequired().HasMaxLength(1);
        builder.Property(q => q.QuizQuestionExplanation).HasColumnName("QUIZ_QUESTION_EXPLANATION").HasColumnType("text");
        builder.Property(q => q.QuizQuestionDifficulty).HasColumnName("QUIZ_QUESTION_DIFFICULTY").HasMaxLength(50);
        builder.Property(q => q.QuizQuestionPoints).HasColumnName("QUIZ_QUESTION_POINTS").IsRequired();
        builder.Property(q => q.QuizQuestionSourceStatus).HasColumnName("QUIZ_QUESTION_SOURCE_STATUS").HasMaxLength(50);

        builder.Property(q => q.QuizQuestionIsActive)
            .HasColumnName("QUIZ_QUESTION_IS_ACTIVE")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(q => q.QuizQuestionCreatedDate).HasColumnName("QUIZ_QUESTION_CREATED_DATE").IsRequired();
        builder.Property(q => q.QuizQuestionUpdatedDate).HasColumnName("QUIZ_QUESTION_UPDATED_DATE").IsRequired();
    }
}
