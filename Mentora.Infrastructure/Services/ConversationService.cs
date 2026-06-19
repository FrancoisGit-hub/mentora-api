using Mentora.Core.DTOs.Conversation;
using Mentora.Core.Entities;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mentora.Infrastructure.Services;

public class ConversationService(MentoraDbContext db) : IConversationService
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

    // ── Internal ───────────────────────────────────────────────────────────────

    private async Task<ConversationDto> GetOrCreateInternalAsync(Guid memberId, Guid coachId, CancellationToken ct)
    {
        // Single query: conversation + its latest message via correlated subquery
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
            return Map(row.Conversation, last);
        }

        // Create new conversation
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
            return Map(existing.Conversation, last);
        }

        return Map(newConv, null);
    }

    private static ConversationDto Map(Conversation c, LastMessageDto? lastMessage) => new(
        ConversationId:  c.ConversationId,
        MemberId:        c.MemberId,
        CoachId:         c.CoachId,
        VisioUrl:        c.ConversationVisioUrl,
        CreatedDate:     c.ConversationCreatedDate,
        LastMessageDate: c.ConversationLastMessageDate,
        LastMessage:     lastMessage);
}
