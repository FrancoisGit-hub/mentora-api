namespace Mentora.Core.Enums;

/// <summary>
/// Lifecycle state of a session voucher.
/// Persisted as VARCHAR(20) UPPERCASE: AVAILABLE / RESERVED / CONSUMED.
/// </summary>
public enum VoucherStatus
{
    Available,
    Reserved,
    Consumed
}
