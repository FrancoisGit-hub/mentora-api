using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Conversation;

/// <param name="ConversationId">Unique identifier of the conversation.</param>
/// <param name="MemberId">Identifier of the member in this conversation.</param>
/// <param name="CoachId">Identifier of the coach in this conversation.</param>
/// <param name="ActiveVisioSession">
/// The currently joinable VISIO session for this member/coach pair, if any — only populated
/// from 15 minutes before the session's start through its end. Null otherwise. Not a
/// permanent room: this window closes.
/// </param>
/// <param name="CreatedDate">Date the conversation was first created (UTC).</param>
/// <param name="LastMessageDate">Date of the most recent message, or null if none yet.</param>
/// <param name="LastMessage">The most recent message in the conversation, or null if none yet.</param>
public sealed record ConversationDto(
    Guid ConversationId,
    Guid MemberId,
    Guid CoachId,
    ActiveVisioSessionDto? ActiveVisioSession,
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
