using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class EquipmentItemConfiguration : IEntityTypeConfiguration<EquipmentItem>
{
    public void Configure(EntityTypeBuilder<EquipmentItem> builder)
    {
        builder.ToTable("EQUIPMENT");

        builder.HasKey(e => e.EquipmentId);
        builder.Property(e => e.EquipmentId)
            .HasColumnName("EQUIPMENT_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        // Unique partial index (WHERE NOT NULL) and the legacy-code CHECK are raw SQL in the
        // Lot9_ExerciseCatalog migration.
        builder.Property(e => e.EquipmentSourceCode)
            .HasColumnName("EQUIPMENT_SOURCE_CODE")
            .HasMaxLength(10);

        builder.Property(e => e.EquipmentName)
            .HasColumnName("EQUIPMENT_NAME")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.EquipmentCategory)
            .HasColumnName("EQUIPMENT_CATEGORY")
            .HasMaxLength(60);

        builder.Property(e => e.EquipmentSubcategory)
            .HasColumnName("EQUIPMENT_SUBCATEGORY")
            .HasMaxLength(60);

        builder.Property(e => e.EquipmentLegacyCode)
            .HasColumnName("EQUIPMENT_LEGACY_CODE")
            .IsRequired()
            .HasMaxLength(40)
            .HasDefaultValue("OTHER");

        builder.Property(e => e.EquipmentIsActive)
            .HasColumnName("EQUIPMENT_IS_ACTIVE")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.EquipmentCreatedDate)
            .HasColumnName("EQUIPMENT_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.EquipmentUpdatedDate)
            .HasColumnName("EQUIPMENT_UPDATED_DATE")
            .IsRequired();

        builder.HasIndex(e => e.EquipmentName)
            .IsUnique()
            .HasDatabaseName("UX_EQUIPMENT_NAME");
    }
}
