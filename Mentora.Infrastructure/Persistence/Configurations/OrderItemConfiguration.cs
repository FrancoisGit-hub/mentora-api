using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("ORDER_ITEMS");

        builder.HasKey(e => e.OrderItemId);
        builder.Property(e => e.OrderItemId)
            .HasColumnName("ORDER_ITEM_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.OrderId)
            .HasColumnName("ORDER_ITEM_ORDER_ID")
            .IsRequired();

        // Weak references — no FK constraints; snapshot semantics post-purchase
        builder.Property(e => e.ProductId)
            .HasColumnName("ORDER_ITEM_PRODUCT_ID")
            .IsRequired();

        builder.Property(e => e.PackId)
            .HasColumnName("ORDER_ITEM_PACK_ID");

        // Snapshot fields
        builder.Property(e => e.ProductName)
            .HasColumnName("ORDER_ITEM_PRODUCT_NAME")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.UnitPriceEuros)
            .HasColumnName("ORDER_ITEM_UNIT_PRICE_EUROS")
            .IsRequired()
            .HasColumnType("numeric(10,2)");

        builder.Property(e => e.Quantity)
            .HasColumnName("ORDER_ITEM_QUANTITY")
            .IsRequired();

        builder.Property(e => e.LineTotalEuros)
            .HasColumnName("ORDER_ITEM_LINE_TOTAL_EUROS")
            .IsRequired()
            .HasColumnType("numeric(10,2)");

        var offerTypeConverter = new ValueConverter<OfferType, string>(
            v => OfferTypeToDb(v),
            v => OfferTypeFromDb(v));

        builder.Property(e => e.OfferType)
            .HasColumnName("ORDER_ITEM_OFFER_TYPE")
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion(offerTypeConverter);

        builder.Property(e => e.DurationMinutes)
            .HasColumnName("ORDER_ITEM_DURATION_MINUTES")
            .IsRequired();

        var sportConverter = new ValueConverter<Sport, string>(
            v => SportToDb(v),
            v => SportFromDb(v));

        builder.Property(e => e.Sport)
            .HasColumnName("ORDER_ITEM_SPORT")
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion(sportConverter)
            .HasDefaultValueSql("'TRAINING'");

        builder.Property(e => e.OrderItemOriginalUnitPriceEuros)
            .HasColumnName("ORDER_ITEM_ORIGINAL_UNIT_PRICE_EUROS")
            .IsRequired()
            .HasColumnType("numeric(10,2)")
            .HasDefaultValue(0m);

        builder.Property(e => e.OrderItemPackDiscountPercentApplied)
            .HasColumnName("ORDER_ITEM_PACK_DISCOUNT_PERCENT_APPLIED")
            .HasColumnType("numeric(5,2)");

        builder.HasOne(e => e.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
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
}
