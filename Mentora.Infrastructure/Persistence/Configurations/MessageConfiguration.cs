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

        // Optimised for paginated reads: newest messages first per conversation
        builder.HasIndex(e => new { e.ConversationId, e.MessageSentDate })
            .IsDescending(false, true)
            .HasDatabaseName("IX_MESSAGES_CONVERSATION_SENT_DESC");

        builder.HasOne(e => e.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
