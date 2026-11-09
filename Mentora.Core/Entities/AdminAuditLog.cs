using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

/// <summary>
/// Lot 12.2b — one row per real change made through the admin back-office (table ADMIN_AUDIT_LOG).
/// Written in the same SaveChanges (same transaction) as the change itself.
/// </summary>
public class AdminAuditLog
{
    public Guid AdminAuditLogId { get; set; }

    public Guid AdminAuditLogAdminUserId { get; set; }
    public AdminUser AdminUser { get; set; } = null!;

    public AdminAuditAction AdminAuditLogAction { get; set; }
    public AdminAuditEntityType AdminAuditLogEntityType { get; set; }
    public Guid AdminAuditLogEntityId { get; set; }

    /// <summary>jsonb. Created: the stored values with ids. Updated: [{ field, before, after }] with request field names. Activation: { isActive: { before, after } }.</summary>
    public string AdminAuditLogChanges { get; set; } = null!;

    public DateTime AdminAuditLogCreatedDate { get; set; }
}
