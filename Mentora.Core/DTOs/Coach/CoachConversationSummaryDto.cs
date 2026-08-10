using Mentora.Core.DTOs.Conversation;

namespace Mentora.Core.DTOs.Coach;

/// <summary>One entry in the coach's aggregated conversation inbox.</summary>
/// <param name="ConversationId">Identifier of the conversation, or null if the member has never exchanged a message with the coach.</param>
/// <param name="MemberId">Identifier of the member.</param>
/// <param name="MemberFirstName">First name of the member.</param>
/// <param name="MemberLastName">Last name of the member.</param>
/// <param name="UnreadCount">Count of messages sent by the member that the coach has not yet read.</param>
/// <param name="LastMessage">The most recent message in the conversation, or null if none yet.</param>
/// <param name="LastMessageDate">Date of the most recent message, or null if none yet.</param>
public record CoachConversationSummaryDto(
    Guid? ConversationId,
    Guid MemberId,
    string MemberFirstName,
    string MemberLastName,
    int UnreadCount,
    LastMessageDto? LastMessage,
    DateTime? LastMessageDate);
