using Mentora.Core.DTOs.Member;
using Mentora.Core.DTOs.Voucher;

namespace Mentora.Core.Interfaces;

public interface IVoucherService
{
    /// <summary>
    /// Lists vouchers for the member, grouped by coach, ordered by MEMBER_COACHES.StartedAt ASC.
    /// When <paramref name="statusFilter"/> is null, defaults to AVAILABLE.
    /// Throws <see cref="InvalidOperationException"/> (→ 400) for unrecognised status strings.
    /// </summary>
    Task<IReadOnlyList<MemberVouchersByCoachGroupDto>> ListForMemberAsync(
        Guid memberId, string? statusFilter, CancellationToken ct);

    Task<VoucherResponse> GetForMemberAsync(
        Guid memberId, Guid voucherId, CancellationToken ct);
}
