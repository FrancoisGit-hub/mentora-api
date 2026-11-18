using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("MESSAGES");

        builder.HasKey(e => e.MessageId);
        builder.Property(e => e.MessageId)
            .HasColumnName("MESSAGE_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.ConversationId)
            .HasColumnName("CONVERSATION_ID")
            .IsRequired();

        builder.Property(e => e.MessageContent)
            .HasColumnName("MESSAGE_CONTENT")
            .HasColumnType("text")
            .IsRequired();

        // MEMBER / COACH stored as UPPERCASE varchar
        var senderTypeConverter = new ValueConverter<MessageSenderType, string>(
            v => v == MessageSenderType.Member ? "MEMBER" : "COACH",
            v => v == "MEMBER" ? MessageSenderType.Member : MessageSenderType.Coach
        );

        builder.Property(e => e.MessageSenderType)
            .HasColumnName("MESSAGE_SENDER_TYPE")
            .IsRequired()
            .HasMaxLength(10)
            .HasConversion(senderTypeConverter);

        builder.Property(e => e.MessageSenderId)
            .HasColumnName("MESSAGE_SENDER_ID")
            .IsRequired();

        builder.Property(e => e.MessageIsRead)
            .HasColumnName("MESSAGE_IS_READ")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.MessageSentDate)
            .HasColumnName("MESSAGE_SENT_DATE")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.MessageReadDate)
            .HasColumnName("MESSAGE_READ_DATE");

        // Lot 13.2: TEXT / AUDIO. The 'TEXT' default fills the rows written before the lot.
        var messageTypeConverter = new ValueConverter<MessageType, string>(
            v => MessageTypeToDb(v),
            v => MessageTypeFromDb(v));

        builder.Property(e => e.MessageType)
            .HasColumnName("MESSAGE_TYPE")
            .IsRequired()
            .HasMaxLength(10)
            .HasConversion(messageTypeConverter)
            .HasDefaultValueSql("'TEXT'")
            // Text is the CLR default (0). Without a sentinel EF would omit the column on insert for
            // Text and rely on the database default; -1 is never a real value, so EF always sends it
            // (same pattern as ExerciseVideoConfiguration).
            .HasSentinel((MessageType)(-1));

        // Lot 13.2: audio fields, all null for a TEXT message. Consistency between MESSAGE_TYPE,
        // MESSAGE_CONTENT and these columns is enforced by the raw SQL CHECK constraints
        // CK_MESSAGES_TYPE and CK_MESSAGES_AUDIO_FIELDS (migration Lot13_2_VoiceMessages).
        builder.Property(e => e.MessageAudioPath)
            .HasColumnName("MESSAGE_AUDIO_PATH")
            .HasMaxLength(200);

        builder.Property(e => e.MessageAudioContentType)
            .HasColumnName("MESSAGE_AUDIO_CONTENT_TYPE")
            .HasMaxLength(20);

        builder.Property(e => e.MessageAudioDurationMs)
            .HasColumnName("MESSAGE_AUDIO_DURATION_MS");

        builder.Property(e => e.MessageAudioSizeBytes)
            .HasColumnName("MESSAGE_AUDIO_SIZE_BYTES");

        // jsonb array of integers; EnableDynamicJson() is already wired in the data source builder.
        builder.Property(e => e.MessageAudioWaveform)
            .HasColumnName("MESSAGE_AUDIO_WAVEFORM")
            .HasColumnType("jsonb");

        // Optimised for paginated reads: newest messages first per conversation
        builder.HasIndex(e => new { e.ConversationId, e.MessageSentDate })
            .IsDescending(false, true)
            .HasDatabaseName("IX_MESSAGES_CONVERSATION_SENT_DESC");

        builder.HasOne(e => e.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static string MessageTypeToDb(MessageType v)
    {
        if (v == MessageType.Text)  return "TEXT";
        if (v == MessageType.Audio) return "AUDIO";
        throw new InvalidOperationException($"Unknown MessageType value {(int)v}.");
    }

    private static MessageType MessageTypeFromDb(string v)
    {
        if (v == "TEXT")  return MessageType.Text;
        if (v == "AUDIO") return MessageType.Audio;
        throw new InvalidOperationException($"Unknown MESSAGE_TYPE value '{v}'.");
    }
}
