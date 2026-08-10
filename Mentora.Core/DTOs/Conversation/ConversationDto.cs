using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Conversation;

/// <param name="ConversationId">Unique identifier of the conversation.</param>
/// <param name="MemberId">Identifier of the member in this conversation.</param>
/// <param name="CoachId">Identifier of the coach in this conversation.</param>
/// <param name="VisioUrl">
/// Permanent Jitsi room URL for this member/coach pair, computed at read time from the
/// conversation ID. Not time-boxed — this room does not expire and has no joinable window,
/// unlike the per-session VISIO URLs on <see cref="Mentora.Core.DTOs.Session.SessionResponse"/>.
/// </param>
/// <param name="CreatedDate">Date the conversation was first created (UTC).</param>
/// <param name="LastMessageDate">Date of the most recent message, or null if none yet.</param>
/// <param name="LastMessage">The most recent message in the conversation, or null if none yet.</param>
public sealed record ConversationDto(
    Guid ConversationId,
    Guid MemberId,
    Guid CoachId,
    string? VisioUrl,
    DateTime CreatedDate,
    DateTime? LastMessageDate,
    LastMessageDto? LastMessage
);

public sealed record LastMessageDto(
    Guid MessageId,
    string Content,
    MessageSenderType SenderType,
    DateTime SentDate,
    bool IsRead
);
