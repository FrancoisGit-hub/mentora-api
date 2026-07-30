using FluentValidation;
using Mentora.Core.DTOs.Conversation;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mentora.Infrastructure.Services;

public class ConversationService(
    MentoraDbContext db,
    IValidator<SendMessageRequestDto> sendMessageValidator) : IConversationService
{
    public async Task<ConversationDto> GetOrCreateForMemberAsync(Guid memberId, Guid coachId, CancellationToken ct)
    {
        var coachExists = await db.Coaches.AnyAsync(c => c.CoachId == coachId, ct);
        if (!coachExists)
            throw new NotFoundException("Coach not found.");

        var isLinked = await db.MemberCoaches
            .AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);
        if (!isLinked)
            throw new ForbiddenException("You are not linked to this coach.");

        return await GetOrCreateInternalAsync(memberId, coachId, ct);
    }

    public async Task<ConversationDto> GetOrCreateForCoachAsync(Guid coachId, Guid memberId, CancellationToken ct)
    {
        var memberExists = await db.Members.AnyAsync(m => m.MemberId == memberId, ct);
        if (!memberExists)
            throw new NotFoundException("Member not found.");

        var isLinked = await db.MemberCoaches
            .AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);
        if (!isLinked)
            throw new ForbiddenException("You are not linked to this member.");

        return await GetOrCreateInternalAsync(memberId, coachId, ct);
    }

    public async Task<MessageListResponseDto> GetMessagesAsync(
        Guid memberId, Guid coachId, DateTime? before, int limit, CancellationToken ct)
    {
        await ValidateAsync(memberId, coachId, ct);

        var conversation = await db.Conversations
            .Where(c => c.MemberId == memberId && c.CoachId == coachId)
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
            return new MessageListResponseDto([], null);

        var query = db.Messages.Where(m => m.ConversationId == conversation.ConversationId);

        if (before.HasValue)
            query = query.Where(m => m.MessageSentDate < before.Value);

        var page = await query
            .OrderByDescending(m => m.MessageSentDate)
            .Take(limit + 1)
            .Select(m => new MessageDto(
                m.MessageId,
                m.ConversationId,
                m.MessageContent,
                m.MessageSenderType,
                m.MessageSenderId,
                m.MessageIsRead,
                m.MessageSentDate,
                m.MessageReadDate))
            .ToListAsync(ct);

        DateTime? nextCursor = null;
        if (page.Count > limit)
        {
            page.RemoveAt(page.Count - 1);
            nextCursor = page[^1].SentDate;
        }

        return new MessageListResponseDto(page, nextCursor);
    }

    public async Task<MessageDto> SendMessageAsync(
        Guid memberId, Guid coachId, MessageSenderType senderType, Guid senderId, string content, CancellationToken ct)
    {
        await sendMessageValidator.ValidateAndThrowAsync(new SendMessageRequestDto(content), ct);
        await ValidateAsync(memberId, coachId, ct);

        var conversation = await GetOrCreateConversationEntityAsync(memberId, coachId, ct);

        using var tx = await db.Database.BeginTransactionAsync(ct);

        var message = new Message
        {
            MessageId         = Guid.NewGuid(),
            ConversationId    = conversation.ConversationId,
            MessageContent    = content.Trim(),
            MessageSenderType = senderType,
            MessageSenderId   = senderId,
            MessageIsRead     = false,
            MessageSentDate   = DateTime.UtcNow,
            MessageReadDate   = null
        };

        db.Messages.Add(message);

        conversation.ConversationLastMessageDate = message.MessageSentDate;
        db.Conversations.Update(conversation);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new MessageDto(
            message.MessageId,
            message.ConversationId,
            message.MessageContent,
            message.MessageSenderType,
            message.MessageSenderId,
            message.MessageIsRead,
            message.MessageSentDate,
            message.MessageReadDate);
    }

    public async Task<int> MarkMessagesAsReadAsync(
        Guid memberId, Guid coachId, MessageSenderType readerType, CancellationToken ct)
    {
        await ValidateAsync(memberId, coachId, ct);

        var conversation = await db.Conversations
            .Where(c => c.MemberId == memberId && c.CoachId == coachId)
            .FirstOrDefaultAsync(ct);

        if (conversation is null) return 0;

        var otherSide = readerType == MessageSenderType.Member
            ? MessageSenderType.Coach
            : MessageSenderType.Member;

        var now = DateTime.UtcNow;
        var markedCount = await db.Messages
            .Where(m => m.ConversationId == conversation.ConversationId
                     && m.MessageSenderType == otherSide
                     && !m.MessageIsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.MessageIsRead, true)
                .SetProperty(m => m.MessageReadDate, now),
                ct);

        return markedCount;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task ValidateAsync(Guid memberId, Guid coachId, CancellationToken ct)
    {
        if (!await db.Coaches.AnyAsync(c => c.CoachId == coachId, ct))
            throw new NotFoundException("Coach not found.");

        if (!await db.Members.AnyAsync(m => m.MemberId == memberId, ct))
            throw new NotFoundException("Member not found.");

        if (!await db.MemberCoaches.AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct))
            throw new ForbiddenException("You are not linked to this coach.");
    }

    private async Task<Conversation> GetOrCreateConversationEntityAsync(Guid memberId, Guid coachId, CancellationToken ct)
    {
        var existing = await db.Conversations
            .FirstOrDefaultAsync(c => c.MemberId == memberId && c.CoachId == coachId, ct);

        if (existing is not null) return existing;

        var newConv = new Conversation
        {
            ConversationId          = Guid.NewGuid(),
            MemberId                = memberId,
            CoachId                 = coachId,
            ConversationCreatedDate = DateTime.UtcNow
        };
        db.Conversations.Add(newConv);

        try
        {
            await db.SaveChangesAsync(ct);
            return newConv;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            // Race condition: another concurrent call created the row first.
            db.ChangeTracker.Clear();
            return await db.Conversations
                .FirstAsync(c => c.MemberId == memberId && c.CoachId == coachId, ct);
        }
    }

    private async Task<ConversationDto> GetOrCreateInternalAsync(Guid memberId, Guid coachId, CancellationToken ct)
    {
        var row = await db.Conversations
            .Where(c => c.MemberId == memberId && c.CoachId == coachId)
            .Select(c => new
            {
                Conversation = c,
                LastMessage = c.Messages
                    .OrderByDescending(m => m.MessageSentDate)
                    .Select(m => new
                    {
                        m.MessageId,
                        m.MessageContent,
                        m.MessageSenderType,
                        m.MessageSentDate,
                        m.MessageIsRead
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (row is not null)
        {
            var last = row.LastMessage is null ? null : new LastMessageDto(
                MessageId:  row.LastMessage.MessageId,
                Content:    row.LastMessage.MessageContent,
                SenderType: row.LastMessage.MessageSenderType,
                SentDate:   row.LastMessage.MessageSentDate,
                IsRead:     row.LastMessage.MessageIsRead);
            return await MapAsync(row.Conversation, last, ct);
        }

        var newConv = new Conversation
        {
            ConversationId          = Guid.NewGuid(),
            MemberId                = memberId,
            CoachId                 = coachId,
            ConversationCreatedDate = DateTime.UtcNow
        };
        db.Conversations.Add(newConv);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            // Race condition: two concurrent first-calls hit the UNIQUE(MEMBER_ID, COACH_ID) constraint.
            // Clear the tracker so the failed entity doesn't block the re-query, then return the winner's row.
            db.ChangeTracker.Clear();
            var existing = await db.Conversations
                .Where(c => c.MemberId == memberId && c.CoachId == coachId)
                .Select(c => new
                {
                    Conversation = c,
                    LastMessage = c.Messages
                        .OrderByDescending(m => m.MessageSentDate)
                        .Select(m => new
                        {
                            m.MessageId,
                            m.MessageContent,
                            m.MessageSenderType,
                            m.MessageSentDate,
                            m.MessageIsRead
                        })
                        .FirstOrDefault()
                })
                .FirstAsync(ct);

            var last = existing.LastMessage is null ? null : new LastMessageDto(
                MessageId:  existing.LastMessage.MessageId,
                Content:    existing.LastMessage.MessageContent,
                SenderType: existing.LastMessage.MessageSenderType,
                SentDate:   existing.LastMessage.MessageSentDate,
                IsRead:     existing.LastMessage.MessageIsRead);
            return await MapAsync(existing.Conversation, last, ct);
        }

        return await MapAsync(newConv, null, ct);
    }

    private async Task<ConversationDto> MapAsync(Conversation c, LastMessageDto? lastMessage, CancellationToken ct)
    {
        var activeVisioSession = await GetActiveVisioSessionAsync(c.MemberId, c.CoachId, ct);

        return new ConversationDto(
            ConversationId:     c.ConversationId,
            MemberId:           c.MemberId,
            CoachId:            c.CoachId,
            ActiveVisioSession: activeVisioSession,
            CreatedDate:        c.ConversationCreatedDate,
            LastMessageDate:    c.ConversationLastMessageDate,
            LastMessage:        lastMessage);
    }

    // A session is "joinable" for the "Join the video call" button from 15 minutes before its
    // start through its end — not a permanent room, and never for cancelled sessions.
    private async Task<ActiveVisioSessionDto?> GetActiveVisioSessionAsync(
        Guid memberId, Guid coachId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        return await db.Sessions
            .Where(s => s.SessionMemberId == memberId
                     && s.SessionCoachId == coachId
                     && s.SessionOfferType == OfferType.Visio
                     && s.SessionStatus != SessionStatus.Cancelled
                     && s.SessionVisioUrl != null
                     && now >= s.SessionScheduledAt.AddMinutes(-15)
                     && now <= s.SessionScheduledAt.AddMinutes(s.SessionDurationMinutes))
            .OrderBy(s => s.SessionScheduledAt)
            .Select(s => new ActiveVisioSessionDto(
                s.SessionId,
                s.SessionVisioUrl!,
                s.SessionScheduledAt,
                s.SessionScheduledAt.AddMinutes(s.SessionDurationMinutes)))
            .FirstOrDefaultAsync(ct);
    }
}
