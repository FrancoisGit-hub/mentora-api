namespace Mentora.Core.DTOs.Conversation;

/// <summary>
/// The session currently eligible for a "Join the video call" button on this conversation.
/// Only surfaced from 15 minutes before <see cref="StartsAt"/> through <see cref="EndsAt"/> —
/// this is NOT a permanent room; outside that window <see cref="ConversationDto.ActiveVisioSession"/>
/// is null.
/// </summary>
public sealed record ActiveVisioSessionDto(
    Guid SessionId,
    string VisioUrl,
    DateTime StartsAt,
    DateTime EndsAt
);
