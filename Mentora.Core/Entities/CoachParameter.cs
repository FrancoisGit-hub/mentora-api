namespace Mentora.Core.Entities;

public class CoachParameter
{
    public Guid CoachParameterId { get; set; }
    public decimal CoachCreditValueEuros { get; set; }
    public int CoachCancellationDelayHours { get; set; }
    public DateTime CoachParameterCreatedDate { get; set; }
    public DateTime CoachParameterUpdatedDate { get; set; }

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;
}
