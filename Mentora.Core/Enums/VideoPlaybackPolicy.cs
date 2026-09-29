namespace Mentora.Core.Enums;

/// <summary>
/// Playback policy of a video asset.
/// Persisted as VARCHAR(10) UPPERCASE: PUBLIC / SIGNED.
/// </summary>
public enum VideoPlaybackPolicy
{
    Public,
    Signed
}
