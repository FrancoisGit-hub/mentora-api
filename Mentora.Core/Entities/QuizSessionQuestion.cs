namespace Mentora.Core.Entities;

/// <summary>A question drawn in a session, with its answer once given (table QUIZ_SESSION_QUESTIONS).</summary>
public class QuizSessionQuestion
{
    public Guid QuizSessionQuestionId { get; set; }

    public Guid QuizSessionQuestionSessionId { get; set; }
    public QuizSession Session { get; set; } = null!;

    public Guid QuizSessionQuestionQuestionId { get; set; }
    public QuizQuestion Question { get; set; } = null!;

    public int QuizSessionQuestionPosition { get; set; }

    // Chosen answer, correctness and points are all NULL (unanswered) or all set.
    public string? QuizSessionQuestionChosenAnswer { get; set; }
    public bool? QuizSessionQuestionIsCorrect { get; set; }
    public int? QuizSessionQuestionPointsEarned { get; set; }

    public DateTime QuizSessionQuestionCreatedDate { get; set; }
    public DateTime QuizSessionQuestionUpdatedDate { get; set; }
}
