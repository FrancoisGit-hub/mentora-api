namespace Mentora.Core.Enums;

/// <summary>
/// State of a quiz session.
/// Persisted as VARCHAR(20) UPPERCASE: IN_PROGRESS / COMPLETED.
/// </summary>
public enum QuizSessionStatus
{
    InProgress,
    Completed
}
