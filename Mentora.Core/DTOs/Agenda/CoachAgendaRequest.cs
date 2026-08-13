namespace Mentora.Core.DTOs.Agenda;

/// <param name="From">Inclusive lower bound. Required.</param>
/// <param name="To">Inclusive upper bound. Required. Range capped at 186 days.</param>
/// <param name="IncludeMemberStandalone">
/// When true, also includes the coach's members' program sessions that carry no booking.
/// Defaults to false: a member's own home-workout day is not an appointment in the coach's
/// working day — that lives on the member detail screen instead.
/// </param>
public record CoachAgendaRequest(DateOnly? From, DateOnly? To, bool IncludeMemberStandalone);
