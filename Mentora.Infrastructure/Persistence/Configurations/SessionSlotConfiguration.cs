using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class SessionSlotConfiguration : IEntityTypeConfiguration<SessionSlot>
{
    public void Configure(EntityTypeBuilder<SessionSlot> builder)
    {
        builder.ToTable("SESSION_SLOTS");

        builder.HasKey(e => e.SessionSlotId);
        builder.Property(e => e.SessionSlotId)
            .HasColumnName("SESSION_SLOT_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.SessionSlotStartDate).HasColumnName("SESSION_SLOT_START_DATE").IsRequired();
        builder.Property(e => e.SessionSlotEndDate).HasColumnName("SESSION_SLOT_END_DATE").IsRequired();
        builder.Property(e => e.SessionSlotPriceEuros).HasColumnName("SESSION_SLOT_PRICE_EUROS").IsRequired().HasColumnType("decimal(8,2)");
        builder.Property(e => e.SessionSlotCreditsRequired).HasColumnName("SESSION_SLOT_CREDITS_REQUIRED").IsRequired();
        builder.Property(e => e.SessionSlotType).HasColumnName("SESSION_SLOT_TYPE").IsRequired().HasMaxLength(20);
        builder.Property(e => e.SessionSlotIsAvailable).HasColumnName("SESSION_SLOT_IS_AVAILABLE").IsRequired();
        builder.Property(e => e.SessionSlotCreatedDate).HasColumnName("SESSION_SLOT_CREATED_DATE").IsRequired();
        builder.Property(e => e.CoachId).HasColumnName("COACH_ID").IsRequired();

        builder.HasOne(e => e.Coach)
            .WithMany(c => c.SessionSlots)
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
