namespace Mentora.Core.Enums;

/// <summary>
/// Persisted as VARCHAR(10) UPPERCASE: MEMBER / COACH. Identifies which role a role-scoped
/// record (e.g. a refresh token) was issued for. Deliberately distinct from
/// <see cref="MessageSenderType"/> and <see cref="CancelledBy"/>, which share the same shape
/// but a different meaning.
/// </summary>
public enum UserRole
{
    Member,
    Coach
}
