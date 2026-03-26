namespace Mentora.Core.Entities;

public class AgentToken
{
    public Guid AgentTokenId { get; set; }
    public string AgentTokenHash { get; set; } = null!;
    public DateTime AgentTokenCreatedDate { get; set; }
    public DateTime AgentTokenExpirationDate { get; set; }
    public bool AgentTokenIsRevoked { get; set; }

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;
}
