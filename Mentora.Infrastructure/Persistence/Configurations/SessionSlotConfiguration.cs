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
        // Static helpers required — expression trees cannot contain switch expressions
        var offerTypeConverter = new ValueConverter<OfferType, string>(
            v => OfferTypeToDb(v),
            v => OfferTypeFromDb(v)
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

    private static string OfferTypeToDb(OfferType v)
    {
        if (v == OfferType.Visio)           return "VISIO";
        if (v == OfferType.PresentielSolo)  return "PRESENTIEL_SOLO";
        if (v == OfferType.PresentielGroupe) return "PRESENTIEL_GROUPE";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown OfferType value.");
    }

    private static OfferType OfferTypeFromDb(string v)
    {
        if (v == "VISIO")             return OfferType.Visio;
        if (v == "PRESENTIEL_SOLO")   return OfferType.PresentielSolo;
        if (v == "PRESENTIEL_GROUPE") return OfferType.PresentielGroupe;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown OfferType DB value.");
    }
}
