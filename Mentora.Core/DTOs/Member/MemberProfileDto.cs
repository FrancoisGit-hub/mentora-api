namespace Mentora.Core.DTOs.Member;

/// <summary>Snapshot of the authenticated member's profile data.</summary>
/// <param name="MemberId">Unique identifier of the member profile.</param>
/// <param name="UserId">Unique identifier of the linked user account.</param>
/// <param name="FirstName">Member's first name.</param>
/// <param name="LastName">Member's last name.</param>
/// <param name="Email">Member's email address (from the linked user account).</param>
/// <param name="Phone">Member's phone number, or null if not set.</param>
/// <param name="CreatedDate">Date the member profile was created (UTC).</param>
/// <param name="ActivationDate">Date the member first activated their account (UTC), or null if never activated.</param>
/// <param name="HasActivated">True if the member has activated their account at least once.</param>
/// <param name="IsActive">True if the member account is currently active.</param>
public record MemberProfileDto(
    Guid MemberId,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTime CreatedDate,
    DateTime? ActivationDate,
    bool HasActivated,
    bool IsActive);
