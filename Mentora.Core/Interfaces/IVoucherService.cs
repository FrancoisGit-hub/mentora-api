using Mentora.Core.DTOs.Voucher;
using Mentora.Core.Enums;

namespace Mentora.Core.Interfaces;

public interface IVoucherService
{
    Task<IReadOnlyList<VoucherResponse>> ListForMemberAsync(
        Guid memberId, VoucherStatus? statusFilter, Guid? coachIdFilter, CancellationToken ct);

    Task<VoucherResponse> GetForMemberAsync(
        Guid memberId, Guid voucherId, CancellationToken ct);
}
