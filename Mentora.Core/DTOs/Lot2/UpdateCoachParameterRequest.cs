namespace Mentora.Core.DTOs.Lot2;

public record UpdateCoachParameterRequest(
    // identity
    string FirstName,
    string LastName,
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
    // prefs
    string Language,
    bool NotifMessages,
    bool NotifNewBooking,
    bool NotifBookingCancelled,
    bool NotifMarketing
);
