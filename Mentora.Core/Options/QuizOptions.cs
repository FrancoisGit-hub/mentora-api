namespace Mentora.Core.Options;

/// <summary>Settings of the basic quiz (Lot 10.2), section "Quiz".</summary>
public sealed class QuizOptions
{
    public const string SectionName = "Quiz";

    /// <summary>Questions drawn per session (1 to 50).</summary>
    public int QuestionCount { get; init; } = 12;

    /// <summary>Minutes after creation during which a session can still be submitted.</summary>
    public int SessionLifetimeMinutes { get; init; } = 120;

    /// <summary>Anonymous (no Authorization header) requests allowed per window and client IP.</summary>
    public int AnonymousPermitLimit { get; init; } = 30;

    public int AnonymousWindowMinutes { get; init; } = 10;
}
