using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Conversation;

public sealed record MessageDto(
    Guid MessageId,
    Guid ConversationId,
    string Content,
    MessageSenderType SenderType,
    Guid SenderId,
    bool IsRead,
    DateTime SentDate,
    DateTime? ReadDate
);
