using Mentora.Core.DTOs.Quiz;

namespace Mentora.Core.Interfaces;

public interface IQuizBasicService
{
    /// <summary>Draws the configured number of random active questions and opens a session.</summary>
    Task<QuizSessionResponse> CreateSessionAsync(QuizOwner owner, CancellationToken ct);

    /// <summary>Scores a session server-side. Unknown or foreign session: 404.</summary>
    Task<QuizSubmitResponse> SubmitAsync(QuizOwner owner, Guid sessionId, QuizSubmitRequest request, CancellationToken ct);

    /// <summary>Completed sessions of the owner, with totals computed at read time.</summary>
    Task<QuizHistoryResponse> GetHistoryAsync(QuizOwner owner, int limit, string? cursor, CancellationToken ct);
}

public interface IQuizDeviceClaimService
{
    /// <summary>Transfers the sessions of an anonymous device to a coach. One claim per device, ever.</summary>
    Task<QuizClaimDeviceResponse> ClaimAsync(Guid coachId, QuizClaimDeviceRequest request, CancellationToken ct);
}
