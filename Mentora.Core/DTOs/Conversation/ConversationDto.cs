using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Conversation;

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
