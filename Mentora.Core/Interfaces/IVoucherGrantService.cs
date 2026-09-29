using Mentora.Core.DTOs.Voucher;

namespace Mentora.Core.Interfaces;

public interface IVoucherGrantService
{
    /// <summary>
    /// Grants free session vouchers on one of the coach's own products to a member currently
    /// attached to that coach. Persists a zero-amount, already-completed purchase (origin
    /// COACH_GRANT) and generates vouchers with the same logic as a paid member purchase.
    /// </summary>
    Task<VoucherGrantResponse> GrantAsync(
        Guid coachId, Guid memberId, VoucherGrantRequest request, CancellationToken ct);
}
