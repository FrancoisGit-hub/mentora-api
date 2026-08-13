namespace Mentora.Core.DTOs.Agenda;

/// <param name="From">Inclusive lower bound. Required.</param>
/// <param name="To">Inclusive upper bound. Required. Range capped at 186 days.</param>
public record MemberAgendaRequest(DateOnly? From, DateOnly? To);
