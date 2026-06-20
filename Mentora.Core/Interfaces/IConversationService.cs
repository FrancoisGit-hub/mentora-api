using Mentora.Core.DTOs.Conversation;
using Mentora.Core.Enums;

namespace Mentora.Core.Interfaces;

public interface IConversationService
{
    /// <summary>
    /// Returns the conversation between the given member and coach, creating it if it doesn't exist (lazy creation).
    /// Throws <see cref="Mentora.Core.Exceptions.NotFoundException"/> if the coach does not exist.
    /// Throws <see cref="Mentora.Core.Exceptions.ForbiddenException"/> if the member is not linked to the coach.
    /// </summary>
    Task<ConversationDto> GetOrCreateForMemberAsync(Guid memberId, Guid coachId, CancellationToken ct);

    /// <summary>
    /// Returns the conversation between the given coach and member, creating it if it doesn't exist (lazy creation).
    /// Throws <see cref="Mentora.Core.Exceptions.NotFoundException"/> if the member does not exist.
    /// Throws <see cref="Mentora.Core.Exceptions.ForbiddenException"/> if the member is not linked to the coach.
    /// </summary>
    Task<ConversationDto> GetOrCreateForCoachAsync(Guid coachId, Guid memberId, CancellationToken ct);

    /// <summary>
    /// Returns paginated messages for the conversation, most-recent first.
    /// Returns an empty list if no conversation row exists yet (read-only — does not create).
    /// </summary>
    Task<MessageListResponseDto> GetMessagesAsync(
        Guid memberId,
        Guid coachId,
        DateTime? before,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Sends a message in the conversation, lazy-creating the conversation row if needed.
    /// </summary>
    Task<MessageDto> SendMessageAsync(
        Guid memberId,
        Guid coachId,
        MessageSenderType senderType,
        Guid senderId,
        string content,
        CancellationToken ct);

    /// <summary>
    /// Marks all unread messages from the other party as read.
    /// Returns the count of messages actually updated (0 if no conversation exists yet).
    /// </summary>
    Task<int> MarkMessagesAsReadAsync(
        Guid memberId,
        Guid coachId,
        MessageSenderType readerType,
        CancellationToken ct);

    /// <summary>
    /// Sets (or resets) the visio URL of the conversation between the given coach and member.
    /// Pass null to revert to the auto-generated Jitsi URL.
    /// Throws <see cref="Mentora.Core.Exceptions.NotFoundException"/> if the member doesn't exist.
    /// Throws <see cref="Mentora.Core.Exceptions.ForbiddenException"/> if the member is not linked to the coach.
    /// </summary>
    Task<ConversationDto> SetVisioUrlAsync(
        Guid coachId,
        Guid memberId,
        string? url,
        CancellationToken ct);
}
