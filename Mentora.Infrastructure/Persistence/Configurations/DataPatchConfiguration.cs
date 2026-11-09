using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

/// <summary>Lot 12.2a — DATA_PATCHES: one row per one-time data patch applied to this database.</summary>
public class DataPatchConfiguration : IEntityTypeConfiguration<DataPatch>
{
    public void Configure(EntityTypeBuilder<DataPatch> builder)
    {
        builder.ToTable("DATA_PATCHES");

        builder.HasKey(e => e.DataPatchName);
        builder.Property(e => e.DataPatchName).HasColumnName("DATA_PATCH_NAME").HasMaxLength(100);
        builder.Property(e => e.DataPatchAppliedDate).HasColumnName("DATA_PATCH_APPLIED_DATE").IsRequired();
    }
}
