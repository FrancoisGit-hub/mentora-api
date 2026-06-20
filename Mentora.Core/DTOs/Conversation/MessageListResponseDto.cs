namespace Mentora.Core.DTOs.Conversation;

public sealed record MessageListResponseDto(
    IReadOnlyList<MessageDto> Messages,
    DateTime? NextCursor
);
