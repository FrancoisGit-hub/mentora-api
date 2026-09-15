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

    /// <summary>
    /// Minutes to wait before the first pass, so a container restart during a
    /// deployment never triggers a delete. Defaults to 15; overridden to 0 in
    /// appsettings.Development.json so local verification runs the first pass
    /// immediately.
    /// </summary>
    public int InitialDelayMinutes { get; init; } = 15;
}
