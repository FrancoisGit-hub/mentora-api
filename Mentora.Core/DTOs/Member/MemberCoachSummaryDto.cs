namespace Mentora.Core.DTOs.Member;

/// <summary>Summary of a coach as seen from the member's perspective, including billing parameters.</summary>
/// <param name="CoachId">Unique identifier of the coach.</param>
/// <param name="FirstName">Coach's first name.</param>
/// <param name="LastName">Coach's last name.</param>
/// <param name="Phone">Coach's phone number, or null if not set.</param>
/// <param name="IsActive">True if the coach account is currently active.</param>
/// <param name="CreatedDate">Date the coach profile was created (UTC).</param>
/// <param name="HourlyRateEuros">Coach's session hourly rate in euros.</param>
/// <param name="CancellationDelayHours">Number of hours before a session within which a member cancellation is considered late.</param>
public record MemberCoachSummaryDto(
    Guid CoachId,
    string FirstName,
    string LastName,
    string? Phone,
    bool IsActive,
    DateTime CreatedDate,
    decimal HourlyRateEuros,
    int CancellationDelayHours);
