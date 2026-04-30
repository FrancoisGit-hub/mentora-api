namespace Mentora.Core.Entities;

public class CoachParameter
{
    public Guid CoachParameterId { get; set; }
    public decimal CoachParameterHourlyRateEuros { get; set; }
    public int CoachParameterCancellationDelayHours { get; set; }
    public DateTime CoachParameterCreatedDate { get; set; }
    public DateTime CoachParameterUpdatedDate { get; set; }

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;
}
