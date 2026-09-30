using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

/// <summary>
/// One quiz attempt (table QUIZ_SESSIONS). Owned by a coach, an anonymous device, or both
/// once the device has been claimed by the coach.
/// </summary>
public class QuizSession
{
    public Guid QuizSessionId { get; set; }

    public Guid? QuizSessionCoachId { get; set; }
    public Coach? Coach { get; set; }

    public string? QuizSessionDeviceId { get; set; }

    public QuizSessionStatus QuizSessionStatus { get; set; }
    public int QuizSessionQuestionCount { get; set; }

    // Set together on completion, all NULL while IN_PROGRESS.
    public int? QuizSessionScore { get; set; }
    public int? QuizSessionCorrectCount { get; set; }
    public DateTime? QuizSessionCompletedDate { get; set; }

    public DateTime QuizSessionExpiresDate { get; set; }

    public DateTime QuizSessionCreatedDate { get; set; }
    public DateTime QuizSessionUpdatedDate { get; set; }

    public ICollection<QuizSessionQuestion> Questions { get; set; } = [];
}
