using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class SessionVoucherConfiguration : IEntityTypeConfiguration<SessionVoucher>
{
    public void Configure(EntityTypeBuilder<SessionVoucher> builder)
    {
        builder.ToTable("SESSION_VOUCHERS");

        builder.HasKey(e => e.VoucherId);
        builder.Property(e => e.VoucherId)
            .HasColumnName("VOUCHER_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.OrderItemId)
            .HasColumnName("VOUCHER_ORDER_ITEM_ID")
            .IsRequired();

        builder.Property(e => e.MemberId)
            .HasColumnName("VOUCHER_MEMBER_ID")
            .IsRequired();

        builder.Property(e => e.CoachId)
            .HasColumnName("VOUCHER_COACH_ID")
            .IsRequired();

        // Weak reference — no FK constraint; product may be archived post-purchase
        builder.Property(e => e.ProductId)
            .HasColumnName("VOUCHER_PRODUCT_ID")
            .IsRequired();

        var offerTypeConverter = new ValueConverter<OfferType, string>(
            v => OfferTypeToDb(v),
            v => OfferTypeFromDb(v));

        builder.Property(e => e.OfferType)
            .HasColumnName("VOUCHER_OFFER_TYPE")
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion(offerTypeConverter);

        builder.Property(e => e.DurationMinutes)
            .HasColumnName("VOUCHER_DURATION_MINUTES")
            .IsRequired();

        var sportConverter = new ValueConverter<Sport, string>(
            v => SportToDb(v),
            v => SportFromDb(v));

        builder.Property(e => e.Sport)
            .HasColumnName("VOUCHER_SPORT")
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion(sportConverter)
            .HasDefaultValueSql("'TRAINING'");

        var statusConverter = new ValueConverter<VoucherStatus, string>(
            v => VoucherStatusToDb(v),
            v => VoucherStatusFromDb(v));

        builder.Property(e => e.Status)
            .HasColumnName("VOUCHER_STATUS")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(statusConverter)
            .HasDefaultValueSql("'AVAILABLE'");

        // Nullable Guid — no FK constraint yet; SESSIONS table added in Lot 2.4
        builder.Property(e => e.ReservedSessionId)
            .HasColumnName("VOUCHER_RESERVED_SESSION_ID");

        builder.Property(e => e.CreatedDate)
            .HasColumnName("VOUCHER_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.UpdatedDate)
            .HasColumnName("VOUCHER_UPDATED_DATE")
            .IsRequired();

        // Frequent query: member's vouchers by status
        builder.HasIndex(e => new { e.MemberId, e.Status })
            .HasDatabaseName("IX_SESSION_VOUCHERS_MEMBER_STATUS");

        builder.HasOne(e => e.OrderItem)
            .WithMany()
            .HasForeignKey(e => e.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Coach)
            .WithMany()
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Restrict);

        // Retroactive FK added in Lot 2.4.1 — SESSIONS table now exists.
        // SetNull: if a Session is hard-deleted, the voucher drops its back-pointer
        // rather than blocking the delete.
        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(v => v.ReservedSessionId)
            .HasConstraintName("FK_SESSION_VOUCHERS_RESERVED_SESSION")
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static string OfferTypeToDb(OfferType v)
    {
        if (v == OfferType.Visio)            return "VISIO";
        if (v == OfferType.PresentielSolo)   return "PRESENTIEL_SOLO";
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

    private static string SportToDb(Sport v)
    {
        if (v == Sport.Training) return "TRAINING";
        if (v == Sport.Boxe)     return "BOXE";
        if (v == Sport.Salle)    return "SALLE";
        if (v == Sport.Course)   return "COURSE";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown Sport value.");
    }

    private static Sport SportFromDb(string v)
    {
        if (v == "TRAINING") return Sport.Training;
        if (v == "BOXE")     return Sport.Boxe;
        if (v == "SALLE")    return Sport.Salle;
        if (v == "COURSE")   return Sport.Course;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown Sport DB value.");
    }

    private static string VoucherStatusToDb(VoucherStatus v)
    {
        if (v == VoucherStatus.Available) return "AVAILABLE";
        if (v == VoucherStatus.Reserved)  return "RESERVED";
        if (v == VoucherStatus.Consumed)  return "CONSUMED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown VoucherStatus value.");
    }

    private static VoucherStatus VoucherStatusFromDb(string v)
    {
        if (v == "AVAILABLE") return VoucherStatus.Available;
        if (v == "RESERVED")  return VoucherStatus.Reserved;
        if (v == "CONSUMED")  return VoucherStatus.Consumed;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown VoucherStatus DB value.");
    }
}
