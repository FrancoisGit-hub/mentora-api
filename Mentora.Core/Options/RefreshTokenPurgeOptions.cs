namespace Mentora.Core.Options;

public sealed class RefreshTokenPurgeOptions
{
    public const string SectionName = "RefreshTokenPurge";

    /// <summary>
    /// When true (the default), the purge only logs what it would delete and deletes
    /// nothing. Flip to false deliberately, after reviewing the count-only output
    /// against real data.
    /// </summary>
    public bool CountOnly { get; init; } = true;
}
