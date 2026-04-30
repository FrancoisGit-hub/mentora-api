using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("PRODUCTS", t =>
        {
            t.HasCheckConstraint("CK_PRODUCTS_DURATION_POSITIVE",    "\"PRODUCT_DURATION_MINUTES\" > 0");
            t.HasCheckConstraint("CK_PRODUCTS_PRICE_NON_NEGATIVE",   "\"PRODUCT_PRICE_EUROS\" >= 0");
        });

        builder.HasKey(e => e.ProductId);
        builder.Property(e => e.ProductId)
            .HasColumnName("PRODUCT_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.ProductName)
            .HasColumnName("PRODUCT_NAME")
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(e => e.ProductDescription)
            .HasColumnName("PRODUCT_DESCRIPTION")
            .HasColumnType("text");

        // OfferType — VISIO / PRESENTIEL_SOLO / PRESENTIEL_GROUPE (same pattern as SessionSlotConfiguration)
        var offerTypeConverter = new ValueConverter<OfferType, string>(
            v => OfferTypeToDb(v),
            v => OfferTypeFromDb(v)
        );

        builder.Property(e => e.ProductOfferType)
            .HasColumnName("PRODUCT_OFFER_TYPE")
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion(offerTypeConverter);

        builder.Property(e => e.ProductOfferNature)
            .HasColumnName("PRODUCT_OFFER_NATURE")
            .IsRequired()
            .HasMaxLength(80)
            .HasDefaultValue("CLASSIQUE");

        builder.Property(e => e.ProductDurationMinutes)
            .HasColumnName("PRODUCT_DURATION_MINUTES")
            .IsRequired();

        builder.Property(e => e.ProductPriceEuros)
            .HasColumnName("PRODUCT_PRICE_EUROS")
            .IsRequired()
            .HasColumnType("numeric(8,2)");

        // Sport — TRAINING / BOXE / SALLE / COURSE
        var sportConverter = new ValueConverter<Sport, string>(
            v => SportToDb(v),
            v => SportFromDb(v)
        );

        builder.Property(e => e.ProductSport)
            .HasColumnName("PRODUCT_SPORT")
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion(sportConverter)
            .HasDefaultValueSql("'TRAINING'");

        builder.Property(e => e.ProductLocation)
            .HasColumnName("PRODUCT_LOCATION")
            .HasMaxLength(200);

        builder.Property(e => e.ProductTags)
            .HasColumnName("PRODUCT_TAGS")
            .HasColumnType("jsonb");

        // ProductStatus — DRAFT / PUBLISHED / ARCHIVED
        var statusConverter = new ValueConverter<ProductStatus, string>(
            v => ProductStatusToDb(v),
            v => ProductStatusFromDb(v)
        );

        builder.Property(e => e.ProductStatus)
            .HasColumnName("PRODUCT_STATUS")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(statusConverter)
            .HasDefaultValueSql("'DRAFT'");

        builder.Property(e => e.ProductCreatedDate)
            .HasColumnName("PRODUCT_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.ProductUpdatedDate)
            .HasColumnName("PRODUCT_UPDATED_DATE")
            .IsRequired();

        builder.Property(e => e.OfferProgramId)
            .HasColumnName("OFFER_PROGRAM_ID")
            .IsRequired();

        builder.Property(e => e.CoachId)
            .HasColumnName("COACH_ID")
            .IsRequired();

        builder.HasIndex(e => new { e.CoachId, e.ProductStatus })
            .HasDatabaseName("IX_PRODUCTS_COACH_STATUS");

        builder.HasOne(e => e.Coach)
            .WithMany(c => c.Products)
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.OfferProgram)
            .WithMany(p => p.Products)
            .HasForeignKey(e => e.OfferProgramId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    // ── OfferType helpers (duplicated from SessionSlotConfiguration — private scope) ──

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

    // ── Sport helpers ──────────────────────────────────────────────────────────

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

    // ── ProductStatus helpers ──────────────────────────────────────────────────

    private static string ProductStatusToDb(ProductStatus v)
    {
        if (v == ProductStatus.Draft)     return "DRAFT";
        if (v == ProductStatus.Published) return "PUBLISHED";
        if (v == ProductStatus.Archived)  return "ARCHIVED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ProductStatus value.");
    }

    private static ProductStatus ProductStatusFromDb(string v)
    {
        if (v == "DRAFT")     return ProductStatus.Draft;
        if (v == "PUBLISHED") return ProductStatus.Published;
        if (v == "ARCHIVED")  return ProductStatus.Archived;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ProductStatus DB value.");
    }
}
