namespace Mentora.Core;

/// <summary>
/// Lot 14.1b: the single rule for an optional phone number stored on a member (trimmed, blank = NULL).
/// </summary>
public static class PhoneNormalizer
{
    public static string? Normalize(string? phone)
    {
        var trimmed = phone?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
