using Mentora.Core.DTOs.Member;

namespace Mentora.Core.Interfaces;

/// <summary>Provides the member home-screen aggregate payload (profile + vouchers grouped by coach).</summary>
public interface IMemberMeService
{
    /// <summary>
    /// Returns the full home-screen payload for the given member.
    /// </summary>
    /// <param name="memberId">The authenticated member's identifier.</param>
    /// <param name="consumedSinceDays">
    /// Controls CONSUMED voucher inclusion: 0 = none, N &gt; 0 = last N days by VOUCHER_UPDATED_DATE.
    /// AVAILABLE and RESERVED vouchers are always included.
    /// Must be &gt;= 0; negative values throw <see cref="System.InvalidOperationException"/>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<MemberMeResponseDto> GetAsync(Guid memberId, int consumedSinceDays, CancellationToken ct);
}
