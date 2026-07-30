namespace Mentora.Core.Entities;

public class MemberParameter
{
    public Guid MemberParameterId { get; set; }
    public string MemberParameterLanguage { get; set; } = "FR";
    public bool MemberParameterNotifMessages { get; set; } = true;
    public bool MemberParameterNotifSessionReminders { get; set; } = true;
    public bool MemberParameterNotifMarketing { get; set; } = false;
    public int MemberParameterSessionReminderHoursBefore { get; set; } = 24;
    public DateTime MemberParameterCreatedDate { get; set; }
    public DateTime MemberParameterUpdatedDate { get; set; }

    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
}
