using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("CONVERSATIONS");

        builder.HasKey(e => e.ConversationId);
        builder.Property(e => e.ConversationId)
            .HasColumnName("CONVERSATION_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.MemberId)
            .HasColumnName("MEMBER_ID")
            .IsRequired();

        builder.Property(e => e.CoachId)
            .HasColumnName("COACH_ID")
            .IsRequired();

        builder.Property(e => e.ConversationCreatedDate)
            .HasColumnName("CONVERSATION_CREATED_DATE")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.ConversationLastMessageDate)
            .HasColumnName("CONVERSATION_LAST_MESSAGE_DATE");

        // 1 conversation per (member, coach) couple
        builder.HasIndex(e => new { e.MemberId, e.CoachId })
            .IsUnique()
            .HasDatabaseName("IX_CONVERSATIONS_MEMBER_COACH");

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict so coach history is never wiped when a member is deleted
        builder.HasOne(e => e.Coach)
            .WithMany()
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
