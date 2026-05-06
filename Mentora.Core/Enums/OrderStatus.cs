namespace Mentora.Core.Enums;

/// <summary>
/// Lifecycle state of a purchase order.
/// Persisted as VARCHAR(20) UPPERCASE: PENDING / PAID / EXPIRED / FAILED.
/// </summary>
public enum OrderStatus
{
    Pending,
    Paid,
    Expired,
    Failed
}
