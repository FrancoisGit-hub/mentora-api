namespace Mentora.Core.Interfaces;

/// <summary>
/// Lot 13.3. Startup probe of the audio tooling: ffmpeg, ffprobe and the storage directory. Every flag
/// is false until <see cref="ProbeAsync"/> has run.
/// </summary>
public interface IAudioAvailability
{
    /// <summary>True when ffmpeg, ffprobe and the storage all passed the probe.</summary>
    bool IsAvailable { get; }

    /// <summary>True when "ffmpeg -hide_banner -version" exited with code 0 within 5 seconds.</summary>
    bool FfmpegOk { get; }

    /// <summary>True when "ffprobe -hide_banner -version" exited with code 0 within 5 seconds.</summary>
    bool FfprobeOk { get; }

    /// <summary>True when the storage path is set, absolute, exists and is writable.</summary>
    bool StorageOk { get; }

    /// <summary>
    /// Null when available; otherwise names each failing part and its setting, never a path value.
    /// </summary>
    string? FailureMessage { get; }

    /// <summary>Runs the probe and updates the flags.</summary>
    Task ProbeAsync(CancellationToken cancellationToken);
}
