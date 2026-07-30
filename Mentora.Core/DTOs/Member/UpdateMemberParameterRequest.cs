using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Member;

/// <summary>
/// Deliberately excludes FirstName, LastName, and Email: a member cannot change those
/// identity fields through this endpoint (support-only operation). Their absence from
/// this DTO IS the security mechanism — do not add them "for symmetry" with the response DTO.
/// </summary>
public record UpdateMemberParameterRequest(
    string? Phone,
    Gender? Gender,
    short? HeightCm,
    DateOnly? BirthDate,
    string Language,
    bool NotifMessages,
    bool NotifSessionReminders,
    bool NotifMarketing,
    int SessionReminderHoursBefore
);
