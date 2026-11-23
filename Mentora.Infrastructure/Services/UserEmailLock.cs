namespace Mentora.Infrastructure.Services;

/// <summary>
/// Lot 14.8a (14.G6): the advisory lock every writer that creates a USERS row by email takes as the first
/// statement of its transaction (member add by a coach, coach creation by an admin), so two of them for the
/// same email run one after the other.
/// </summary>
internal static class UserEmailLock
{
    public const string Sql = "SELECT pg_advisory_xact_lock(7141401)";
}
