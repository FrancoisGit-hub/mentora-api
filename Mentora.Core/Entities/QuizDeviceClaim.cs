namespace Mentora.Core.Entities;

/// <summary>Points of an anonymous device credited to a coach. One claim per device, ever (table QUIZ_DEVICE_CLAIMS).</summary>
public class QuizDeviceClaim
{
    public Guid QuizDeviceClaimId { get; set; }

    public string QuizDeviceClaimDeviceId { get; set; } = null!;

    public Guid QuizDeviceClaimCoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    public int QuizDeviceClaimPoints { get; set; }

    public DateTime QuizDeviceClaimCreatedDate { get; set; }
}
