namespace Mentora.Core.DTOs.Lot2;

public record CoachParameterDto(
    // identity
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    // billing
    decimal HourlyRateEuros,
    int CancellationDelayHours,
    // booking
    int MinBookingNoticeHours,
    int MaxBookingHorizonDays,
    bool LateCancellationRefunds,
    bool IsAcceptingNewBookings,
    int DefaultSessionDurationMinutes,
    // delivery
    string? PresentialAddress,
    string? CustomVisioUrl,
    // prefs
    string Language,
    bool NotifMessages,
    bool NotifNewBooking,
    bool NotifBookingCancelled,
    bool NotifMarketing,
    // audit
    DateTime UpdatedAt
);
