using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Conversation;

/// <param name="ConversationId">Unique identifier of the conversation.</param>
/// <param name="MemberId">Identifier of the member in this conversation.</param>
/// <param name="CoachId">Identifier of the coach in this conversation.</param>
/// <param name="VisioUrl">
/// Permanent Jitsi room URL for this member/coach pair, computed at read time from the
/// conversation ID. Not time-boxed — this room does not expire and has no joinable window.
/// Unrelated to <see cref="Mentora.Core.DTOs.Session.SessionResponse"/>.VisioUrl, which is
/// deprecated since Lot 8 (always null) — per-session video calls now go through
/// POST .../sessions/{sessionId}/visio-token (LiveKit), not Jitsi.
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

/// <param name="MessageId">Unique identifier of the message.</param>
/// <param name="Content">Text of the message; always an empty string for an AUDIO message.</param>
/// <param name="SenderType">Side that sent the message (MEMBER or COACH).</param>
/// <param name="SentDate">Date the message was sent (UTC).</param>
/// <param name="IsRead">True once the other side has marked the message as read.</param>
/// <param name="MessageType">Lot 13.2: TEXT or AUDIO.</param>
/// <param name="MediaUrl">
/// Lot 13.2: relative URL of the audio listening route for an AUDIO message, null for a TEXT message.
/// </param>
/// <param name="DurationSeconds">Lot 13.2: audio duration in whole seconds (at least 1), null for a TEXT message.</param>
public sealed record LastMessageDto(
    Guid MessageId,
    string Content,
    MessageSenderType SenderType,
    DateTime SentDate,
    bool IsRead,
    MessageType MessageType,
    string? MediaUrl,
    int? DurationSeconds
);
