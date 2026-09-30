namespace Mentora.Core.Entities;

/// <summary>Multiple-choice question imported from the quiz sheet (table QUIZ_QUESTIONS).</summary>
public class QuizQuestion
{
    public Guid QuizQuestionId { get; set; }

    /// <summary>Code in the source sheet (e.g. QUE-001). Unique.</summary>
    public string QuizQuestionSourceCode { get; set; } = null!;

    public string? QuizQuestionSubjectCode { get; set; }
    public string? QuizQuestionSubject { get; set; }
    public string? QuizQuestionChapterCode { get; set; }
    public string? QuizQuestionChapter { get; set; }
    public string? QuizQuestionCardCode { get; set; }
    public string? QuizQuestionCardTitle { get; set; }

    public string QuizQuestionText { get; set; } = null!;
    public string QuizQuestionAnswerA { get; set; } = null!;
    public string QuizQuestionAnswerB { get; set; } = null!;
    public string QuizQuestionAnswerC { get; set; } = null!;
    public string QuizQuestionAnswerD { get; set; } = null!;

    /// <summary>"A" | "B" | "C" | "D".</summary>
    public string QuizQuestionCorrectAnswer { get; set; } = null!;

    public string? QuizQuestionExplanation { get; set; }

    /// <summary>Free text as in the sheet (Facile, Intermédiaire, Difficile).</summary>
    public string? QuizQuestionDifficulty { get; set; }

    public int QuizQuestionPoints { get; set; }

    /// <summary>Status column of the sheet (Brouillon, ...), stored as-is.</summary>
    public string? QuizQuestionSourceStatus { get; set; }

    public bool QuizQuestionIsActive { get; set; } = true;

    public DateTime QuizQuestionCreatedDate { get; set; }
    public DateTime QuizQuestionUpdatedDate { get; set; }
}
