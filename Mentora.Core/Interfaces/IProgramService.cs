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

    // Coach-side — booking correction (Lot 6.5)
    Task<ProgramSessionBookingResponse> UpdateBookingAsync(
        Guid coachId, Guid programSessionId, UpdateProgramSessionBookingRequest request, CancellationToken ct);

    // Coach-side — completion (Lot 6.6). Records loads on behalf of the member (e.g. during a
    // PRESENTIEL session) and may set CoachNote.
    Task<ProgramSessionResponse> UpdateCompletionByCoachAsync(
        Guid coachId, Guid programSessionId, UpdateProgramSessionCompletionRequest request, CancellationToken ct);

    // Member-side
    Task<ProgramResponse> GetCurrentForMemberAsync(Guid memberId, CancellationToken ct);

    Task<ProgramResponse> GetByIdForMemberAsync(Guid memberId, Guid programId, CancellationToken ct);

    // Member-side — completion (Lot 6.6)
    Task<ProgramSessionResponse> UpdateCompletionByMemberAsync(
        Guid memberId, Guid programSessionId, UpdateProgramSessionCompletionRequest request, CancellationToken ct);
}
