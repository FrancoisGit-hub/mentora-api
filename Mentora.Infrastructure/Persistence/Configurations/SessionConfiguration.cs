using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("SESSIONS");

        builder.HasKey(e => e.SessionId);
        builder.Property(e => e.SessionId)
            .HasColumnName("SESSION_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.SessionStatus).HasColumnName("SESSION_STATUS").IsRequired().HasMaxLength(20);
        builder.Property(e => e.SessionCreditsConsumed).HasColumnName("SESSION_CREDITS_CONSUMED").IsRequired();
        builder.Property(e => e.SessionCancelReason).HasColumnName("SESSION_CANCEL_REASON").HasColumnType("text");
        builder.Property(e => e.SessionCancelledDate).HasColumnName("SESSION_CANCELLED_DATE");
        builder.Property(e => e.SessionCreatedDate).HasColumnName("SESSION_CREATED_DATE").IsRequired();
        builder.Property(e => e.SessionSlotId).HasColumnName("SESSION_SLOT_ID").IsRequired();
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();
        builder.Property(e => e.CoachId).HasColumnName("COACH_ID").IsRequired();

        builder.HasOne(e => e.SessionSlot)
            .WithOne(s => s.Session)
            .HasForeignKey<Session>(e => e.SessionSlotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Coach)
            .WithMany(c => c.Sessions)
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
