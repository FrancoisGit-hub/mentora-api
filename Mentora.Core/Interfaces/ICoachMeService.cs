using Mentora.Core.DTOs.Coach;

namespace Mentora.Core.Interfaces;

/// <summary>Provides the coach home-screen aggregate payload (profile, roster, upcoming sessions, offers, stats).</summary>
public interface ICoachMeService
{
    /// <summary>
    /// Returns the full home-screen payload for the given coach.
    /// </summary>
    /// <param name="coachId">The authenticated coach's identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CoachMeResponseDto> GetAsync(Guid coachId, CancellationToken ct);
}
