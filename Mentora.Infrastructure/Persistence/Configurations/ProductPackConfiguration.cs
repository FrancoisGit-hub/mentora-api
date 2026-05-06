using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ProductPackConfiguration : IEntityTypeConfiguration<ProductPack>
{
    public void Configure(EntityTypeBuilder<ProductPack> builder)
    {
        builder.ToTable("PRODUCT_PACKS", t =>
        {
            t.HasCheckConstraint(
                "CK_PRODUCT_PACKS_PRICE_NON_NEGATIVE",
                "\"PRODUCT_PACK_PRICE_EUROS\" >= 0");
            t.HasCheckConstraint(
                "CK_PRODUCT_PACKS_DISCOUNT_PERCENT_RANGE",
                "\"PRODUCT_PACK_DISCOUNT_PERCENT\" >= 0 AND \"PRODUCT_PACK_DISCOUNT_PERCENT\" <= 100");
        });

        builder.HasKey(e => e.ProductPackId);
        builder.Property(e => e.ProductPackId)
            .HasColumnName("PRODUCT_PACK_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.ProductPackName)
            .HasColumnName("PRODUCT_PACK_NAME")
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(e => e.ProductPackDescription)
            .HasColumnName("PRODUCT_PACK_DESCRIPTION")
            .HasColumnType("text");

        builder.Property(e => e.ProductPackPriceEuros)
            .HasColumnName("PRODUCT_PACK_PRICE_EUROS")
            .IsRequired()
            .HasColumnType("numeric(8,2)");

        builder.Property(e => e.ProductPackDiscountPercent)
            .HasColumnName("PRODUCT_PACK_DISCOUNT_PERCENT")
            .IsRequired()
            .HasColumnType("numeric(5,2)")
            .HasDefaultValue(0m);

        // ProductStatus reused — DRAFT / PUBLISHED / ARCHIVED
        var statusConverter = new ValueConverter<ProductStatus, string>(
            v => ProductStatusToDb(v),
            v => ProductStatusFromDb(v)
        );

        builder.Property(e => e.ProductPackStatus)
            .HasColumnName("PRODUCT_PACK_STATUS")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(statusConverter)
            .HasDefaultValueSql("'DRAFT'");

        builder.Property(e => e.ProductPackCreatedDate)
            .HasColumnName("PRODUCT_PACK_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.ProductPackUpdatedDate)
            .HasColumnName("PRODUCT_PACK_UPDATED_DATE")
            .IsRequired();

        builder.Property(e => e.CoachId)
            .HasColumnName("COACH_ID")
            .IsRequired();

        builder.HasIndex(e => new { e.CoachId, e.ProductPackStatus })
            .HasDatabaseName("IX_PRODUCT_PACKS_COACH_STATUS");

        builder.HasOne(e => e.Coach)
            .WithMany(c => c.ProductPacks)
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
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
