namespace Mentora.Core.Entities;

public class CoachParameter
{
    public Guid CoachParameterId { get; set; }
    public decimal CoachParameterHourlyRateEuros { get; set; } = 50.00m;
    public int CoachParameterCancellationDelayHours { get; set; } = 24;
    public string CoachParameterLanguage { get; set; } = "FR";
    public bool CoachParameterNotifMessages { get; set; } = true;
    public bool CoachParameterNotifNewBooking { get; set; } = true;
    public bool CoachParameterNotifBookingCancelled { get; set; } = true;
    public bool CoachParameterNotifMarketing { get; set; } = false;
    public string? CoachParameterPresentialAddress { get; set; }
    public int CoachParameterMinBookingNoticeHours { get; set; } = 24;
    public int CoachParameterMaxBookingHorizonDays { get; set; } = 90;
    public bool CoachParameterLateCancellationRefunds { get; set; } = false;
    public bool CoachParameterIsAcceptingNewBookings { get; set; } = true;
    public int CoachParameterDefaultSessionDurationMinutes { get; set; } = 60;
    public string? CoachParameterCustomVisioUrl { get; set; }
    public DateTime CoachParameterCreatedDate { get; set; }
    public DateTime CoachParameterUpdatedDate { get; set; }

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;
}
