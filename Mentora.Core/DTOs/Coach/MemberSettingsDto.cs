namespace Mentora.Core.DTOs.Coach;

/// <summary>The member's settings as seen and partially editable by their coach.</summary>
/// <param name="MemberId">Unique identifier of the member.</param>
/// <param name="FirstName">Member's first name. Read-only.</param>
/// <param name="LastName">Member's last name. Read-only.</param>
/// <param name="Email">Member's email address. Read-only.</param>
/// <param name="PresentialAddress">
/// This coach's override address for in-person sessions with this specific member, or null to
/// fall back to the product's own location. The only field this endpoint can write.
/// </param>
public record MemberSettingsDto(
    Guid MemberId,
    string FirstName,
    string LastName,
    string Email,
    string? PresentialAddress
);
