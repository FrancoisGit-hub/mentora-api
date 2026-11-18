using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Conversation;

/// <param name="MessageId">Unique identifier of the message.</param>
/// <param name="ConversationId">Identifier of the conversation the message belongs to.</param>
/// <param name="Content">Text of the message; always an empty string for an AUDIO message.</param>
/// <param name="SenderType">Side that sent the message (MEMBER or COACH).</param>
/// <param name="SenderId">MemberId or CoachId of the sender.</param>
/// <param name="IsRead">True once the other side has marked the message as read.</param>
/// <param name="SentDate">Date the message was sent (UTC).</param>
/// <param name="ReadDate">Date the message was read (UTC), or null.</param>
/// <param name="MessageType">Lot 13.2: TEXT or AUDIO.</param>
/// <param name="MediaUrl">
/// Lot 13.2: relative URL of the audio listening route for an AUDIO message, null for a TEXT message.
/// </param>
/// <param name="DurationSeconds">Lot 13.2: audio duration in whole seconds (at least 1), null for a TEXT message.</param>
/// <param name="Waveform">Lot 13.2: waveform bars (integers 0 to 100), null for a TEXT message.</param>
public sealed record MessageDto(
    Guid MessageId,
    Guid ConversationId,
    string Content,
    MessageSenderType SenderType,
    Guid SenderId,
    bool IsRead,
    DateTime SentDate,
    DateTime? ReadDate,
    MessageType MessageType,
    string? MediaUrl,
    int? DurationSeconds,
    IReadOnlyList<int>? Waveform
);
