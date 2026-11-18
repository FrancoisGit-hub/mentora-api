using Mentora.Core.DTOs.Coach;
using Mentora.Core.DTOs.Conversation;
using Mentora.Core.Enums;

namespace Mentora.Core.Interfaces;

public interface IConversationService
{
    /// <summary>
    /// Returns the conversation between the given member and coach, creating it if it doesn't exist (lazy creation).
    /// Throws <see cref="Mentora.Core.Exceptions.NotFoundException"/> if the member is not linked to the coach
    /// (never <see cref="Mentora.Core.Exceptions.ForbiddenException"/> — a 403 would confirm the coach exists).
    /// </summary>
    Task<ConversationDto> GetOrCreateForMemberAsync(Guid memberId, Guid coachId, CancellationToken ct);

    /// <summary>
    /// Returns the conversation between the given coach and member, creating it if it doesn't exist (lazy creation).
    /// Throws <see cref="Mentora.Core.Exceptions.NotFoundException"/> if the member is not linked to the coach
    /// (never <see cref="Mentora.Core.Exceptions.ForbiddenException"/> — a 403 would confirm the member exists).
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
    /// Lot 13.4: sends a voice message. Checks, in order: the MEMBER_COACHES link (404), the declared
    /// file length (422, key File), the storage (503), the copied bytes capped at
    /// <see cref="Mentora.Core.Messaging.VoiceMessageRules.MaxSizeBytes"/> + 1 (422), the server side
    /// analysis (503 without ffmpeg, 422 per outcome). The duration is always measured by the server.
    /// The stored file never outlives a failed insert and the incoming file is always deleted.
    /// </summary>
    /// <param name="memberId">Member of the conversation.</param>
    /// <param name="coachId">Coach of the conversation.</param>
    /// <param name="senderType">Side of the sender.</param>
    /// <param name="senderId">MemberId or CoachId of the sender.</param>
    /// <param name="content">The uploaded bytes, null when no file was sent.</param>
    /// <param name="length">Declared length of the upload, null when no file was sent.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<MessageDto> SendAudioMessageAsync(
        Guid memberId,
        Guid coachId,
        MessageSenderType senderType,
        Guid senderId,
        Stream? content,
        long? length,
        CancellationToken ct);

    /// <summary>
    /// Lot 13.4: returns the stored relative path and content type of a voice message when the caller is a
    /// participant: the message exists in that conversation with type AUDIO, the caller's memberId equals the
    /// conversation member or the caller's coachId equals the conversation coach, and the MEMBER_COACHES link
    /// still exists. Returns null in every other case (one SQL query, no distinction between the cases).
    /// </summary>
    /// <param name="conversationId">Conversation of the message.</param>
    /// <param name="messageId">The voice message.</param>
    /// <param name="callerMemberId">memberId claim of the caller, if any.</param>
    /// <param name="callerCoachId">coachId claim of the caller, if any.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<(string RelativePath, string ContentType)?> GetAudioFileForParticipantAsync(
        Guid conversationId,
        Guid messageId,
        Guid? callerMemberId,
        Guid? callerCoachId,
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
    /// Returns the coach's aggregated inbox: one entry per member linked via MEMBER_COACHES
    /// (so a member who has never exchanged a message still appears, with a null conversationId),
    /// ordered by lastMessageDate descending, no-message members last. Single query — the round
    /// trip count does not grow with the number of members.
    /// </summary>
    Task<List<CoachConversationSummaryDto>> GetInboxForCoachAsync(Guid coachId, CancellationToken ct);
}
