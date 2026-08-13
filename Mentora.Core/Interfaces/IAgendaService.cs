using Mentora.Core.DTOs.Agenda;

namespace Mentora.Core.Interfaces;

public interface IAgendaService
{
    Task<List<AgendaEntryResponse>> GetForCoachAsync(Guid coachId, CoachAgendaRequest request, CancellationToken ct);

    Task<List<AgendaEntryResponse>> GetForMemberAsync(Guid memberId, MemberAgendaRequest request, CancellationToken ct);
}
