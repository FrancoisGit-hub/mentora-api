using Mentora.Core.DTOs.Conversation;

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
}
