using Mentora.Core.DTOs.Member;

namespace Mentora.Core.Interfaces;

public interface IMemberParameterService
{
    Task<MemberParameterDto> GetAsync(Guid memberId, CancellationToken ct);
    Task<MemberParameterDto> UpdateAsync(Guid memberId, UpdateMemberParameterRequest request, CancellationToken ct);
}
