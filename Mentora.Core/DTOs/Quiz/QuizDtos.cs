namespace Mentora.Core.DTOs.Quiz;

/// <summary>Owner of a quiz request: the coach in coach mode, the anonymous device otherwise.</summary>
/// <param name="CoachId">Coach id from the JWT; <c>null</c> in device mode.</param>
/// <param name="DeviceId">Value of <c>X-Device-Id</c>; only used (and validated) in device mode.</param>
public sealed record QuizOwner(Guid? CoachId, string? DeviceId)
{
    public bool IsCoachMode => CoachId.HasValue;
}

/// <summary>One answer choice of a question.</summary>
/// <param name="Key">"A" to "D".</param>
/// <param name="Text">Answer text.</param>
public record QuizAnswerOptionDto(string Key, string Text);

/// <summary>A drawn question, with the correct answer and explanation (the client corrects locally).</summary>
/// <param name="Position">1-based position in the session.</param>
/// <param name="QuestionId">Question identifier.</param>
/// <param name="Text">Question text.</param>
/// <param name="Answers">The four choices, A to D.</param>
/// <param name="CorrectAnswer">"A" to "D".</param>
/// <param name="Explanation">Explanation shown after answering, if any.</param>
/// <param name="Points">Points earned for a correct answer.</param>
/// <param name="Difficulty">Difficulty label as in the sheet.</param>
/// <param name="Subject">Subject label.</param>
/// <param name="Chapter">Chapter label.</param>
public record QuizSessionQuestionDto(
    int Position,
    Guid QuestionId,
    string Text,
    IReadOnlyList<QuizAnswerOptionDto> Answers,
    string CorrectAnswer,
    string? Explanation,
    int Points,
    string? Difficulty,
    string? Subject,
    string? Chapter);

/// <summary>A newly created quiz session.</summary>
/// <param name="SessionId">Session identifier.</param>
/// <param name="ExpiresAt">Last instant at which the session can be submitted (UTC).</param>
/// <param name="QuestionCount">Number of questions drawn.</param>
/// <param name="Questions">Questions ordered by position.</param>
public record QuizSessionResponse(
    Guid SessionId,
    DateTime ExpiresAt,
    int QuestionCount,
    IReadOnlyList<QuizSessionQuestionDto> Questions);

/// <summary>One answer of a submission.</summary>
/// <param name="QuestionId">Question drawn in the session.</param>
/// <param name="ChosenAnswer">"A", "B", "C" or "D" (uppercase).</param>
public record QuizAnswerRequest(Guid QuestionId, string? ChosenAnswer);

/// <summary>Submission of all the answers of a session.</summary>
/// <param name="Answers">Exactly one answer per drawn question.</param>
public record QuizSubmitRequest(List<QuizAnswerRequest>? Answers);

/// <summary>Correction of one question.</summary>
/// <param name="Position">Position in the session.</param>
/// <param name="QuestionId">Question identifier.</param>
/// <param name="ChosenAnswer">Answer given.</param>
/// <param name="CorrectAnswer">Right answer.</param>
/// <param name="IsCorrect">Whether the answer given is right.</param>
/// <param name="PointsEarned">Question points if correct, else 0.</param>
public record QuizResultItemDto(
    int Position,
    Guid QuestionId,
    string ChosenAnswer,
    string CorrectAnswer,
    bool IsCorrect,
    int PointsEarned);

/// <summary>Score of a submitted session.</summary>
/// <param name="SessionId">Session identifier.</param>
/// <param name="Score">Sum of the points earned.</param>
/// <param name="CorrectCount">Number of correct answers.</param>
/// <param name="QuestionCount">Number of questions.</param>
/// <param name="CompletedAt">Completion instant (UTC).</param>
/// <param name="TotalPoints">Owner total (sum of the scores of the owner's completed sessions) after this session.</param>
/// <param name="Results">Correction ordered by position.</param>
public record QuizSubmitResponse(
    Guid SessionId,
    int Score,
    int CorrectCount,
    int QuestionCount,
    DateTime CompletedAt,
    int TotalPoints,
    IReadOnlyList<QuizResultItemDto> Results);

/// <summary>A completed session in the history.</summary>
/// <param name="SessionId">Session identifier.</param>
/// <param name="CompletedAt">Completion instant (UTC).</param>
/// <param name="Score">Score of the session.</param>
/// <param name="CorrectCount">Number of correct answers.</param>
/// <param name="QuestionCount">Number of questions.</param>
public record QuizHistoryItemDto(
    Guid SessionId,
    DateTime CompletedAt,
    int Score,
    int CorrectCount,
    int QuestionCount);

/// <summary>Completed sessions of the owner, newest first.</summary>
/// <param name="TotalPoints">Sum of the scores of all the owner's completed sessions.</param>
/// <param name="CompletedCount">Number of the owner's completed sessions.</param>
/// <param name="Items">Page of sessions (completed date desc, then id desc).</param>
/// <param name="NextCursor">Opaque cursor for the next page, <c>null</c> on the last page.</param>
public record QuizHistoryResponse(
    int TotalPoints,
    int CompletedCount,
    IReadOnlyList<QuizHistoryItemDto> Items,
    string? NextCursor);

/// <summary>A question of a session as replayed by the review endpoint.</summary>
/// <param name="Position">1-based position in the session.</param>
/// <param name="QuestionId">Question identifier.</param>
/// <param name="Text">Question text (read live from the question bank).</param>
/// <param name="Answers">The four choices, A to D.</param>
/// <param name="ChosenAnswer">Answer given, <c>null</c> while the session is in progress.</param>
/// <param name="CorrectAnswer">"A" to "D".</param>
/// <param name="IsCorrect">Whether the answer given is right, <c>null</c> while the session is in progress.</param>
/// <param name="PointsEarned">Points earned, <c>null</c> while the session is in progress.</param>
/// <param name="Points">Points earned for a correct answer.</param>
/// <param name="Explanation">Explanation, if any.</param>
/// <param name="Difficulty">Difficulty label as in the sheet.</param>
/// <param name="Subject">Subject label.</param>
/// <param name="Chapter">Chapter label.</param>
public record QuizSessionReviewQuestionDto(
    int Position,
    Guid QuestionId,
    string Text,
    IReadOnlyList<QuizAnswerOptionDto> Answers,
    string? ChosenAnswer,
    string CorrectAnswer,
    bool? IsCorrect,
    int? PointsEarned,
    int Points,
    string? Explanation,
    string? Difficulty,
    string? Subject,
    string? Chapter);

/// <summary>A quiz session replayed from its id, in progress or completed.</summary>
/// <param name="SessionId">Session identifier.</param>
/// <param name="Status">"IN_PROGRESS" or "COMPLETED".</param>
/// <param name="IsExpired">True when the session is still in progress and past its expiry (computed at read time).</param>
/// <param name="QuestionCount">Number of questions drawn.</param>
/// <param name="Score">Stored score, <c>null</c> while in progress.</param>
/// <param name="CorrectCount">Stored number of correct answers, <c>null</c> while in progress.</param>
/// <param name="CreatedAt">Creation instant (UTC).</param>
/// <param name="ExpiresAt">Last instant at which the session can be submitted (UTC).</param>
/// <param name="CompletedAt">Completion instant (UTC), <c>null</c> while in progress.</param>
/// <param name="Questions">Questions ordered by position.</param>
public record QuizSessionReviewResponse(
    Guid SessionId,
    string Status,
    bool IsExpired,
    int QuestionCount,
    int? Score,
    int? CorrectCount,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? CompletedAt,
    IReadOnlyList<QuizSessionReviewQuestionDto> Questions);

/// <summary>Claim of an anonymous device by a coach.</summary>
/// <param name="DeviceId">Device id (16 to 64 characters, ASCII letters, digits and hyphen).</param>
public record QuizClaimDeviceRequest(string? DeviceId);

/// <summary>Result of a device claim.</summary>
/// <param name="DeviceId">Claimed device.</param>
/// <param name="ClaimedPoints">Sum of the scores of the completed sessions transferred to the coach.</param>
/// <param name="ClaimedSessions">Number of completed sessions transferred.</param>
/// <param name="TotalPoints">Coach total after the claim.</param>
public record QuizClaimDeviceResponse(
    string DeviceId,
    int ClaimedPoints,
    int ClaimedSessions,
    int TotalPoints);

// Typed success envelopes { success, data, error, statusCode } (Swagger models).

/// <summary>Envelope of a created session.</summary>
public record QuizSessionEnvelope(bool Success, QuizSessionResponse Data, string? Error, int StatusCode);

/// <summary>Envelope of a submission result.</summary>
public record QuizSubmitEnvelope(bool Success, QuizSubmitResponse Data, string? Error, int StatusCode);

/// <summary>Envelope of a session review.</summary>
public record QuizSessionReviewEnvelope(bool Success, QuizSessionReviewResponse Data, string? Error, int StatusCode);

/// <summary>Envelope of the history.</summary>
public record QuizHistoryEnvelope(bool Success, QuizHistoryResponse Data, string? Error, int StatusCode);

/// <summary>Envelope of a device claim.</summary>
public record QuizClaimDeviceEnvelope(bool Success, QuizClaimDeviceResponse Data, string? Error, int StatusCode);
