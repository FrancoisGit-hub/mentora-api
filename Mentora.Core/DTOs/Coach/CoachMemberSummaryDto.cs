namespace Mentora.Core.DTOs.Coach;

/// <summary>Summary of one of the coach's active members, for the home-screen roster.</summary>
/// <param name="MemberId">Unique identifier of the member.</param>
/// <param name="FirstName">Member's first name.</param>
/// <param name="LastName">Member's last name.</param>
/// <param name="Email">Member's email address (from the linked user account).</param>
/// <param name="Phone">Member's phone number, or null if not set.</param>
public record CoachMemberSummaryDto(
    Guid MemberId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone);
