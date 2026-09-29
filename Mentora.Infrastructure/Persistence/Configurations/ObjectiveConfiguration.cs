using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ObjectiveConfiguration : IEntityTypeConfiguration<Objective>
{
    public void Configure(EntityTypeBuilder<Objective> builder)
    {
        builder.ToTable("OBJECTIVES");

        builder.HasKey(e => e.ObjectiveId);
        builder.Property(e => e.ObjectiveId)
            .HasColumnName("OBJECTIVE_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.ObjectiveName)
            .HasColumnName("OBJECTIVE_NAME")
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(e => e.ObjectiveCreatedDate)
            .HasColumnName("OBJECTIVE_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.ObjectiveUpdatedDate)
            .HasColumnName("OBJECTIVE_UPDATED_DATE")
            .IsRequired();

        builder.HasIndex(e => e.ObjectiveName)
            .IsUnique()
            .HasDatabaseName("UX_OBJECTIVES_NAME");
    }
}
