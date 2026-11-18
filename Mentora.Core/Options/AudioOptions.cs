namespace Mentora.Core.Options;

/// <summary>
/// Lot 13.3. Voice message audio settings, from the environment variables Audio__* only (no "Audio"
/// section in appsettings.json). The numeric limits are checked in every environment; outside
/// Development <see cref="StoragePath"/> is required and must be an absolute path. Failure messages
/// name the setting and never contain a value.
/// </summary>
public sealed class AudioOptions
{
    /// <summary>Configuration section name ("Audio").</summary>
    public const string SectionName = "Audio";

    /// <summary>Smallest allowed value of <see cref="MaxConcurrentDecodes"/>.</summary>
    public const int MinConcurrentDecodes = 1;

    /// <summary>Largest allowed value of <see cref="MaxConcurrentDecodes"/>.</summary>
    public const int MaxConcurrentDecodesLimit = 4;

    /// <summary>Smallest allowed value of <see cref="DecodeTimeoutSeconds"/>.</summary>
    public const int MinDecodeTimeoutSeconds = 1;

    /// <summary>Largest allowed value of <see cref="DecodeTimeoutSeconds"/>.</summary>
    public const int MaxDecodeTimeoutSeconds = 60;

    /// <summary>Path or command name of the ffmpeg executable.</summary>
    public string FfmpegPath { get; init; } = "ffmpeg";

    /// <summary>Path or command name of the ffprobe executable.</summary>
    public string FfprobePath { get; init; } = "ffprobe";

    /// <summary>Absolute directory where the voice message files are stored.</summary>
    public string StoragePath { get; init; } = "";

    /// <summary>Maximum number of audio analyses running at the same time (1 to 4).</summary>
    public int MaxConcurrentDecodes { get; init; } = 2;

    /// <summary>Timeout of one analysis (probe and decode together), in seconds (1 to 60).</summary>
    public int DecodeTimeoutSeconds { get; init; } = 10;

    /// <summary>Returns a failure message (naming the setting, never a value) or null when valid.</summary>
    public string? Validate(bool isDevelopment)
    {
        if (MaxConcurrentDecodes is < MinConcurrentDecodes or > MaxConcurrentDecodesLimit)
            return $"Audio:MaxConcurrentDecodes must be between {MinConcurrentDecodes} and {MaxConcurrentDecodesLimit}.";
        if (DecodeTimeoutSeconds is < MinDecodeTimeoutSeconds or > MaxDecodeTimeoutSeconds)
            return $"Audio:DecodeTimeoutSeconds must be between {MinDecodeTimeoutSeconds} and {MaxDecodeTimeoutSeconds}.";

        if (isDevelopment)
            return null; // Development may run without audio: the audio services then answer 503.

        if (string.IsNullOrWhiteSpace(StoragePath))
            return "Audio:StoragePath must be set as an environment variable outside Development.";
        if (!Path.IsPathFullyQualified(StoragePath))
            return "Audio:StoragePath must be an absolute path.";
        return null;
    }
}
