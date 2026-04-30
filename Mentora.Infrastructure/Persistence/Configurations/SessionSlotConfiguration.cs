using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class SessionSlotConfiguration : IEntityTypeConfiguration<SessionSlot>
{
    public void Configure(EntityTypeBuilder<SessionSlot> builder)
    {
        builder.ToTable("SESSION_SLOTS", t =>
            t.HasCheckConstraint(
                "CK_SESSION_SLOTS_END_AFTER_START",
                "\"SESSION_SLOT_END_DATE\" > \"SESSION_SLOT_START_DATE\""));

        builder.HasKey(e => e.SessionSlotId);
        builder.Property(e => e.SessionSlotId)
            .HasColumnName("SESSION_SLOT_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.SessionSlotStartDate)
            .HasColumnName("SESSION_SLOT_START_DATE")
            .IsRequired();

        builder.Property(e => e.SessionSlotEndDate)
            .HasColumnName("SESSION_SLOT_END_DATE")
            .IsRequired();

        // Stored as uppercase string: VISIO / PRESENTIEL_SOLO / PRESENTIEL_GROUPE
        var offerTypeConverter = new ValueConverter<OfferType, string>(
            v => v switch
            {
                OfferType.Visio           => "VISIO",
                OfferType.PresentielSolo  => "PRESENTIEL_SOLO",
                OfferType.PresentielGroupe => "PRESENTIEL_GROUPE",
                _                         => throw new ArgumentOutOfRangeException(nameof(v), v, null)
            },
            v => v switch
            {
                "VISIO"            => OfferType.Visio,
                "PRESENTIEL_SOLO"  => OfferType.PresentielSolo,
                "PRESENTIEL_GROUPE" => OfferType.PresentielGroupe,
                _                  => throw new ArgumentOutOfRangeException(nameof(v), v, null)
            }
        );

        builder.Property(e => e.SessionSlotOfferType)
            .HasColumnName("SESSION_SLOT_OFFER_TYPE")
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion(offerTypeConverter);

        builder.Property(e => e.SessionSlotDurationMinutes)
            .HasColumnName("SESSION_SLOT_DURATION_MINUTES")
            .IsRequired();

        builder.Property(e => e.SessionSlotIsAvailable)
            .HasColumnName("SESSION_SLOT_IS_AVAILABLE")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.SessionSlotCreatedDate)
            .HasColumnName("SESSION_SLOT_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.CoachId)
            .HasColumnName("COACH_ID")
            .IsRequired();

        builder.HasOne(e => e.Coach)
            .WithMany(c => c.SessionSlots)
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
