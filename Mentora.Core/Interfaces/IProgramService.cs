using Mentora.Core.DTOs.Program;

namespace Mentora.Core.Interfaces;

public interface IProgramService
{
    // Coach-side
    Task<List<ProgramHeaderResponse>> ListForCoachAsync(
        Guid coachId, Guid memberId, bool includeArchived, CancellationToken ct);

    Task<ProgramResponse> GetByIdForCoachAsync(Guid coachId, Guid programId, CancellationToken ct);

    Task<AssignProgramResponse> AssignAsync(
        Guid coachId, Guid memberId, AssignProgramRequest request, CancellationToken ct);

    Task<ProgramResponse> UpdateAsync(
        Guid coachId, Guid programId, UpdateProgramRequest request, CancellationToken ct);

    Task DeleteAsync(Guid coachId, Guid programId, CancellationToken ct);

    // Member-side
    Task<ProgramResponse> GetCurrentForMemberAsync(Guid memberId, CancellationToken ct);

    Task<ProgramResponse> GetByIdForMemberAsync(Guid memberId, Guid programId, CancellationToken ct);
}
