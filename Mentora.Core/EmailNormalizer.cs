namespace Mentora.Core;

/// <summary>
/// Lot 14.1 (14.A3): the single rule for an email entering the system (trim, then lower invariant).
/// Lookups compare lower("USER_EMAIL") with this value, so rows stored with capitals still match.
/// </summary>
public static class EmailNormalizer
{
    public static string Normalize(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();
}
