using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class MuscleGroupItemConfiguration : IEntityTypeConfiguration<MuscleGroupItem>
{
    public void Configure(EntityTypeBuilder<MuscleGroupItem> builder)
    {
        builder.ToTable("MUSCLE_GROUPS");

        builder.HasKey(e => e.MuscleGroupId);
        builder.Property(e => e.MuscleGroupId)
            .HasColumnName("MUSCLE_GROUP_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.MuscleGroupName)
            .HasColumnName("MUSCLE_GROUP_NAME")
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(e => e.MuscleGroupSortOrder)
            .HasColumnName("MUSCLE_GROUP_SORT_ORDER")
            .IsRequired();

        // Legacy MuscleGroup enum DB value; the CHECK (13 values) is raw SQL in the
        // Lot9_ExerciseCatalog migration.
        builder.Property(e => e.MuscleGroupLegacyCode)
            .HasColumnName("MUSCLE_GROUP_LEGACY_CODE")
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(e => e.MuscleGroupCreatedDate)
            .HasColumnName("MUSCLE_GROUP_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.MuscleGroupUpdatedDate)
            .HasColumnName("MUSCLE_GROUP_UPDATED_DATE")
            .IsRequired();

        builder.HasIndex(e => e.MuscleGroupName)
            .IsUnique()
            .HasDatabaseName("UX_MUSCLE_GROUPS_NAME");
    }
}
