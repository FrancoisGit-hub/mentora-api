namespace Mentora.Core.Entities;

public class CreditBalance
{
    public Guid CreditBalanceId { get; set; }
    public int CreditBalanceAmount { get; set; }
    public DateTime CreditBalanceUpdatedDate { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;
}
