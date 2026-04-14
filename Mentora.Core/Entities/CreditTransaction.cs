namespace Mentora.Core.Entities;

public class CreditTransaction
{
    public Guid CreditTransactionId { get; set; }
    public string CreditTransactionType { get; set; } = null!;
    public int CreditTransactionAmount { get; set; }
    public decimal? CreditTransactionEuros { get; set; }
    public DateTime CreditTransactionDate { get; set; }

    public Guid? SessionId { get; set; }
    public Session? Session { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;
}
