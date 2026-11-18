using FluentValidation;
using FluentValidation.Results;
using Mentora.Core.DTOs.Coach;
using Mentora.Core.DTOs.Conversation;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Messaging;
using Mentora.Core.Validators.Conversation;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mentora.Infrastructure.Services;

public class ConversationService(
    MentoraDbContext db,
    IValidator<SendMessageRequestDto> sendMessageValidator,
    IValidator<SendAudioMessageRequest> sendAudioMessageValidator,
    IVisioUrlGenerator visioUrlGenerator,
    IVoiceAudioProcessor audioProcessor,
    IVoiceMessageStorage audioStorage) : IConversationService
{
    private const int CopyBufferBytes = 81_920;
    public async Task<ConversationDto> GetOrCreateForMemberAsync(Guid memberId, Guid coachId, CancellationToken ct)
    {
        // 404, never 403 — same pattern as GetOrCreateForCoachAsync: a member on an unrelated
        // coach must never learn the coach exists.
        var isLinked = await db.MemberCoaches
            .AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);
        if (!isLinked)
            throw new NotFoundException($"Coach {coachId} not found.");

        return await GetOrCreateInternalAsync(memberId, coachId, ct);
    }

    public async Task<ConversationDto> GetOrCreateForCoachAsync(Guid coachId, Guid memberId, CancellationToken ct)
    {
        // 404, never 403 — same pattern as CoachMemberSettingsService.LoadLinkedAsync: an
        // unattached coach must never learn the member exists.
        var isLinked = await db.MemberCoaches
            .AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);
        if (!isLinked)
            throw new NotFoundException($"Member {memberId} not found.");

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

        // One SQL query selecting the stored columns; MediaUrl and DurationSeconds are computed in
        // memory by ToMessageDto (never stored).
        var rows = await query
            .OrderByDescending(m => m.MessageSentDate)
            .Take(limit + 1)
            .Select(m => new
            {
                m.MessageId,
                m.ConversationId,
                m.MessageContent,
                m.MessageSenderType,
                m.MessageSenderId,
                m.MessageIsRead,
                m.MessageSentDate,
                m.MessageReadDate,
                m.MessageType,
                m.MessageAudioDurationMs,
                m.MessageAudioWaveform
            })
            .ToListAsync(ct);

        var page = rows
            .Select(m => ToMessageDto(
                m.MessageId,
                m.ConversationId,
                m.MessageContent,
                m.MessageSenderType,
                m.MessageSenderId,
                m.MessageIsRead,
                m.MessageSentDate,
                m.MessageReadDate,
                m.MessageType,
                m.MessageAudioDurationMs,
                m.MessageAudioWaveform))
            .ToList();

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
            MessageReadDate   = null,
            // Lot 13.2: this path stays text only (the voice upload is a separate route from 13.3).
            MessageType       = MessageType.Text
        };

        db.Messages.Add(message);

        conversation.ConversationLastMessageDate = message.MessageSentDate;
        db.Conversations.Update(conversation);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToMessageDto(
            message.MessageId,
            message.ConversationId,
            message.MessageContent,
            message.MessageSenderType,
            message.MessageSenderId,
            message.MessageIsRead,
            message.MessageSentDate,
            message.MessageReadDate,
            message.MessageType,
            message.MessageAudioDurationMs,
            message.MessageAudioWaveform);
    }

    public async Task<MessageDto> SendAudioMessageAsync(
        Guid memberId, Guid coachId, MessageSenderType senderType, Guid senderId,
        Stream? content, long? length, CancellationToken ct)
    {
        // 13.J order: link (404), declared length (422), storage (503), copied bytes (422), analysis (503 / 422).
        await ValidateAsync(memberId, coachId, ct);
        await sendAudioMessageValidator.ValidateAndThrowAsync(
            new SendAudioMessageRequest(content is null ? null : length), ct);

        var incomingPath = audioStorage.CreateIncomingFilePath();
        try
        {
            // Never trust the declared length alone: copy at most MaxSizeBytes + 1 bytes.
            var copiedBytes = await CopyCappedAsync(content!, incomingPath, VoiceMessageRules.MaxSizeBytes + 1L, ct);
            if (copiedBytes > VoiceMessageRules.MaxSizeBytes)
            {
                audioStorage.DeleteIncoming(incomingPath);
                throw FileValidationError(SendAudioMessageRequestValidator.FileTooLargeMessage);
            }

            var analysis = await audioProcessor.AnalyzeAsync(incomingPath, ct);
            if (analysis.Outcome != AudioAnalysisOutcome.Ok)
            {
                audioStorage.DeleteIncoming(incomingPath);
                throw FileValidationError(AnalysisFailureMessage(analysis.Outcome));
            }

            var conversation = await GetOrCreateConversationEntityAsync(memberId, coachId, ct);
            var messageId = Guid.NewGuid();
            var relativePath = audioStorage.RelativePathFor(conversation.ConversationId, messageId);
            await audioStorage.CommitAsync(incomingPath, relativePath, ct);

            var message = new Message
            {
                MessageId               = messageId,
                ConversationId          = conversation.ConversationId,
                MessageContent          = "",
                MessageSenderType       = senderType,
                MessageSenderId         = senderId,
                MessageIsRead           = false,
                MessageSentDate         = DateTime.UtcNow,
                MessageReadDate         = null,
                MessageType             = MessageType.Audio,
                MessageAudioPath        = relativePath,
                MessageAudioContentType = analysis.ContentType,
                MessageAudioDurationMs  = analysis.DurationMs,
                MessageAudioSizeBytes   = (int)copiedBytes,
                MessageAudioWaveform    = analysis.Waveform!.ToList()
            };

            try
            {
                using var tx = await db.Database.BeginTransactionAsync(ct);

                db.Messages.Add(message);
                conversation.ConversationLastMessageDate = message.MessageSentDate;
                db.Conversations.Update(conversation);

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                // 13.J: the committed file never outlives a failed insert.
                audioStorage.Delete(relativePath);
                throw;
            }

            return ToMessageDto(
                message.MessageId,
                message.ConversationId,
                message.MessageContent,
                message.MessageSenderType,
                message.MessageSenderId,
                message.MessageIsRead,
                message.MessageSentDate,
                message.MessageReadDate,
                message.MessageType,
                message.MessageAudioDurationMs,
                message.MessageAudioWaveform);
        }
        finally
        {
            // Every path: the incoming file is gone after the call (moved by the commit, or deleted here).
            if (File.Exists(incomingPath))
                audioStorage.DeleteIncoming(incomingPath);
        }
    }

    public async Task<(string RelativePath, string ContentType)?> GetAudioFileForParticipantAsync(
        Guid conversationId, Guid messageId, Guid? callerMemberId, Guid? callerCoachId, CancellationToken ct)
    {
        if (callerMemberId is null && callerCoachId is null)
            return null;

        // One query: message in that conversation, type AUDIO, caller is its member or its coach,
        // and the MEMBER_COACHES link of the conversation still exists.
        var row = await db.Messages
            .Where(m => m.MessageId == messageId
                     && m.ConversationId == conversationId
                     && m.MessageType == MessageType.Audio)
            .Where(m => (callerMemberId != null && m.Conversation.MemberId == callerMemberId)
                     || (callerCoachId != null && m.Conversation.CoachId == callerCoachId))
            .Where(m => db.MemberCoaches.Any(mc => mc.MemberId == m.Conversation.MemberId
                                                && mc.CoachId == m.Conversation.CoachId))
            .Select(m => new { m.MessageAudioPath, m.MessageAudioContentType })
            .FirstOrDefaultAsync(ct);

        if (row?.MessageAudioPath is null || row.MessageAudioContentType is null)
            return null;

        return (row.MessageAudioPath, row.MessageAudioContentType);
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

    public async Task<List<CoachConversationSummaryDto>> GetInboxForCoachAsync(Guid coachId, CancellationToken ct)
    {
        // MEMBER_COACHES is the base — not CONVERSATIONS — so a member who has never exchanged a
        // message still appears (LEFT JOIN to CONVERSATIONS). LastMessage/UnreadCount are
        // correlated subqueries on the conversation's own Messages navigation, so the whole inbox
        // is one round trip regardless of how many members the coach has — no N+1.
        var query =
            from mc in db.MemberCoaches
            where mc.CoachId == coachId
            join c in db.Conversations
                on new { MemberId = mc.MemberId, CoachId = mc.CoachId }
                equals new { MemberId = c.MemberId, CoachId = c.CoachId }
                into convJoin
            from c in convJoin.DefaultIfEmpty()
            select new
            {
                mc.MemberId,
                mc.Member.MemberFirstName,
                mc.Member.MemberLastName,
                ConversationId   = (Guid?)c.ConversationId,
                LastMessageDate  = c.ConversationLastMessageDate,
                LastMessage = c.Messages
                    .OrderByDescending(m => m.MessageSentDate)
                    .Select(m => new
                    {
                        m.MessageId,
                        m.ConversationId,
                        m.MessageContent,
                        m.MessageSenderType,
                        m.MessageSentDate,
                        m.MessageIsRead,
                        m.MessageType,
                        m.MessageAudioDurationMs
                    })
                    .FirstOrDefault(),
                // Unread = sent BY THE MEMBER (MessageSenderType.Member) and MESSAGE_IS_READ is false.
                UnreadCount = c.Messages.Count(m =>
                    m.MessageSenderType == MessageSenderType.Member && !m.MessageIsRead)
            };

        var rows = await query.ToListAsync(ct);

        return rows
            .Select(r => new CoachConversationSummaryDto(
                ConversationId:   r.ConversationId,
                MemberId:         r.MemberId,
                MemberFirstName:  r.MemberFirstName,
                MemberLastName:   r.MemberLastName,
                UnreadCount:      r.UnreadCount,
                LastMessage:      r.LastMessage is null ? null : ToLastMessageDto(
                                       r.LastMessage.MessageId,
                                       r.LastMessage.ConversationId,
                                       r.LastMessage.MessageContent,
                                       r.LastMessage.MessageSenderType,
                                       r.LastMessage.MessageSentDate,
                                       r.LastMessage.MessageIsRead,
                                       r.LastMessage.MessageType,
                                       r.LastMessage.MessageAudioDurationMs),
                LastMessageDate:  r.LastMessageDate))
            // Nullable DateTime's default comparer treats null as the smallest value, so members
            // with no message sink to the end under a descending sort.
            .OrderByDescending(d => d.LastMessageDate)
            .ToList();
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>Copies at most <paramref name="cap"/> bytes into a new file; returns the bytes written.</summary>
    private static async Task<long> CopyCappedAsync(Stream source, string targetPath, long cap, CancellationToken ct)
    {
        var buffer = new byte[CopyBufferBytes];
        long total = 0;
        await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            CopyBufferBytes, FileOptions.Asynchronous);
        while (total < cap)
        {
            var toRead = (int)Math.Min(buffer.Length, cap - total);
            var read = await source.ReadAsync(buffer.AsMemory(0, toRead), ct);
            if (read == 0)
                break;
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            total += read;
        }
        return total;
    }

    private static ValidationException FileValidationError(string message)
        => new(new[] { new ValidationFailure(SendAudioMessageRequestValidator.FilePropertyName, message) });

    private static string AnalysisFailureMessage(AudioAnalysisOutcome outcome)
    {
        if (outcome == AudioAnalysisOutcome.UnsupportedFormat)
            return "Unsupported audio format (AAC in ADTS or M4A only).";
        if (outcome == AudioAnalysisOutcome.TooShort)
            return "Audio is too short (minimum 0.3 s).";
        if (outcome == AudioAnalysisOutcome.TooLong)
            return "Audio is too long (maximum 120 s).";
        return "Audio file could not be decoded.";
    }

    private async Task ValidateAsync(Guid memberId, Guid coachId, CancellationToken ct)
    {
        // 404, never 403 — same pattern as GetOrCreateForCoachAsync/GetOrCreateForMemberAsync:
        // the (memberId, coachId) link check IS the existence check, in one query.
        var isLinked = await db.MemberCoaches
            .AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);
        if (!isLinked)
            throw new NotFoundException("Conversation not found.");
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
                        m.ConversationId,
                        m.MessageContent,
                        m.MessageSenderType,
                        m.MessageSentDate,
                        m.MessageIsRead,
                        m.MessageType,
                        m.MessageAudioDurationMs
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (row is not null)
        {
            var last = row.LastMessage is null ? null : ToLastMessageDto(
                row.LastMessage.MessageId,
                row.LastMessage.ConversationId,
                row.LastMessage.MessageContent,
                row.LastMessage.MessageSenderType,
                row.LastMessage.MessageSentDate,
                row.LastMessage.MessageIsRead,
                row.LastMessage.MessageType,
                row.LastMessage.MessageAudioDurationMs);
            return Map(row.Conversation, last);
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
                            m.ConversationId,
                            m.MessageContent,
                            m.MessageSenderType,
                            m.MessageSentDate,
                            m.MessageIsRead,
                            m.MessageType,
                            m.MessageAudioDurationMs
                        })
                        .FirstOrDefault()
                })
                .FirstAsync(ct);

            var last = existing.LastMessage is null ? null : ToLastMessageDto(
                existing.LastMessage.MessageId,
                existing.LastMessage.ConversationId,
                existing.LastMessage.MessageContent,
                existing.LastMessage.MessageSenderType,
                existing.LastMessage.MessageSentDate,
                existing.LastMessage.MessageIsRead,
                existing.LastMessage.MessageType,
                existing.LastMessage.MessageAudioDurationMs);
            return Map(existing.Conversation, last);
        }

        return Map(newConv, null);
    }

    private ConversationDto Map(Conversation c, LastMessageDto? lastMessage)
    {
        return new ConversationDto(
            ConversationId:  c.ConversationId,
            MemberId:        c.MemberId,
            CoachId:         c.CoachId,
            VisioUrl:        visioUrlGenerator.GenerateForConversation(c.ConversationId),
            CreatedDate:     c.ConversationCreatedDate,
            LastMessageDate: c.ConversationLastMessageDate,
            LastMessage:     lastMessage);
    }

    // Lot 13.2: builds the wire fields of a message from its stored columns. For AUDIO, MediaUrl and
    // DurationSeconds are computed here (VoiceMessageRules), never stored; for TEXT they stay null.
    private static MessageDto ToMessageDto(
        Guid messageId, Guid conversationId, string content, MessageSenderType senderType, Guid senderId,
        bool isRead, DateTime sentDate, DateTime? readDate,
        MessageType messageType, int? audioDurationMs, List<int>? audioWaveform)
    {
        var isAudio = messageType == MessageType.Audio;
        return new MessageDto(
            messageId,
            conversationId,
            content,
            senderType,
            senderId,
            isRead,
            sentDate,
            readDate,
            messageType,
            isAudio ? VoiceMessageRules.MediaUrl(conversationId, messageId) : null,
            isAudio && audioDurationMs.HasValue ? VoiceMessageRules.DurationSeconds(audioDurationMs.Value) : null,
            isAudio ? audioWaveform : null);
    }

    private static LastMessageDto ToLastMessageDto(
        Guid messageId, Guid conversationId, string content, MessageSenderType senderType,
        DateTime sentDate, bool isRead, MessageType messageType, int? audioDurationMs)
    {
        var isAudio = messageType == MessageType.Audio;
        return new LastMessageDto(
            messageId,
            content,
            senderType,
            sentDate,
            isRead,
            messageType,
            isAudio ? VoiceMessageRules.MediaUrl(conversationId, messageId) : null,
            isAudio && audioDurationMs.HasValue ? VoiceMessageRules.DurationSeconds(audioDurationMs.Value) : null);
    }
}
