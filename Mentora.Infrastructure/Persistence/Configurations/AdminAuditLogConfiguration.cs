using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

/// <summary>
/// Lot 12.2b — ADMIN_AUDIT_LOG. CK_ADMIN_AUDIT_LOG_ACTION and CK_ADMIN_AUDIT_LOG_ENTITY_TYPE are created
/// by the migration (migrationBuilder.Sql), not declared here.
/// </summary>
public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("ADMIN_AUDIT_LOG");

        builder.HasKey(e => e.AdminAuditLogId);
        builder.Property(e => e.AdminAuditLogId)
            .HasColumnName("ADMIN_AUDIT_LOG_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.AdminAuditLogAdminUserId).HasColumnName("ADMIN_AUDIT_LOG_ADMIN_USER_ID").IsRequired();

        builder.Property(e => e.AdminAuditLogAction)
            .HasColumnName("ADMIN_AUDIT_LOG_ACTION")
            .IsRequired()
            .HasMaxLength(40)
            .HasConversion(new ValueConverter<AdminAuditAction, string>(
                v => AdminAuditMappings.ActionToDb(v),
                v => AdminAuditMappings.ActionFromDb(v)));

        builder.Property(e => e.AdminAuditLogEntityType)
            .HasColumnName("ADMIN_AUDIT_LOG_ENTITY_TYPE")
            .IsRequired()
            .HasMaxLength(40)
            .HasConversion(new ValueConverter<AdminAuditEntityType, string>(
                v => AdminAuditMappings.EntityTypeToDb(v),
                v => AdminAuditMappings.EntityTypeFromDb(v)));

        builder.Property(e => e.AdminAuditLogEntityId).HasColumnName("ADMIN_AUDIT_LOG_ENTITY_ID").IsRequired();

        builder.Property(e => e.AdminAuditLogChanges)
            .HasColumnName("ADMIN_AUDIT_LOG_CHANGES")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.AdminAuditLogCreatedDate).HasColumnName("ADMIN_AUDIT_LOG_CREATED_DATE").IsRequired();

        // Restrict: an admin account that has acted can never be deleted.
        builder.HasOne(e => e.AdminUser)
            .WithMany()
            .HasForeignKey(e => e.AdminAuditLogAdminUserId)
            .HasConstraintName("FK_ADMIN_AUDIT_LOG_ADMIN_USER")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.AdminAuditLogAdminUserId)
            .HasDatabaseName("IX_ADMIN_AUDIT_LOG_ADMIN_USER");

        builder.HasIndex(e => new { e.AdminAuditLogEntityType, e.AdminAuditLogEntityId, e.AdminAuditLogCreatedDate })
            .HasDatabaseName("IX_ADMIN_AUDIT_LOG_ENTITY");

        builder.HasIndex(e => e.AdminAuditLogCreatedDate)
            .HasDatabaseName("IX_ADMIN_AUDIT_LOG_CREATED_DATE");
    }
}
