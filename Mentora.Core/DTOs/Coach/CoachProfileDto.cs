namespace Mentora.Core.DTOs.Coach;

/// <summary>Snapshot of the authenticated coach's profile data.</summary>
/// <param name="CoachId">Unique identifier of the coach profile.</param>
/// <param name="UserId">Unique identifier of the linked user account.</param>
/// <param name="FirstName">Coach's first name.</param>
/// <param name="LastName">Coach's last name.</param>
/// <param name="Email">Coach's email address (from the linked user account).</param>
/// <param name="Phone">Coach's phone number, or null if not set.</param>
/// <param name="CreatedDate">Date the coach profile was created (UTC).</param>
/// <param name="IsActive">True if the coach account is currently active.</param>
/// <param name="HourlyRateEuros">Coach's session hourly rate in euros.</param>
/// <param name="CancellationDelayHours">Number of hours before a session within which a member cancellation is considered late.</param>
public record CoachProfileDto(
    Guid CoachId,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTime CreatedDate,
    bool IsActive,
    decimal HourlyRateEuros,
    int CancellationDelayHours);
