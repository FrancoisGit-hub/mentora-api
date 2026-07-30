using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Member;

public record MemberParameterDto(
    // identity
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    // physical
    Gender? Gender,
    short? HeightCm,
    DateOnly? BirthDate,
    // prefs
    string Language,
    bool NotifMessages,
    bool NotifSessionReminders,
    bool NotifMarketing,
    int SessionReminderHoursBefore,
    // audit
    DateTime UpdatedAt
);
