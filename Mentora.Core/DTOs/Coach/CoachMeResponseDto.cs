using Mentora.Core.DTOs.Catalog;

namespace Mentora.Core.DTOs.Coach;

/// <summary>Full coach home-screen payload: profile, active members, upcoming sessions, published offers, and stats.</summary>
/// <param name="Profile">The coach's profile data.</param>
/// <param name="ActiveMembers">Active members linked to this coach, ordered by last name then first name.</param>
/// <param name="UpcomingSessions">Scheduled sessions in the next 7 days, ordered by scheduled date ascending.</param>
/// <param name="Offers">Published products in the coach's catalog.</param>
/// <param name="Stats">Activity and revenue stats for the current and previous month.</param>
public record CoachMeResponseDto(
    CoachProfileDto Profile,
    IReadOnlyList<CoachMemberSummaryDto> ActiveMembers,
    IReadOnlyList<CoachUpcomingSessionDto> UpcomingSessions,
    IReadOnlyList<ProductResponse> Offers,
    CoachStatsDto Stats);
