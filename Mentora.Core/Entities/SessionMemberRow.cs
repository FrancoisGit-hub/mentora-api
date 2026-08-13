namespace Mentora.Core.Entities;

/// <summary>
/// Keyless read model over V_SESSION_MEMBERS — the single "who is on this session" source,
/// covering both individual sessions (SESSIONS.SESSION_MEMBER_ID) and group registrations
/// (SESSION_PARTICIPANTS, excluding CANCELLED). Every "sessions of member X" / "is member X on
/// session Y" read goes through this instead of SESSIONS.SESSION_MEMBER_ID directly.
/// </summary>
public class SessionMemberRow
{
    public Guid SessionId { get; set; }
    public Guid MemberId { get; set; }
}
