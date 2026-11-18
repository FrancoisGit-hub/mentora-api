using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services.Audio;

/// <summary>
/// Lot 13.3. Probes ffmpeg and ffprobe ("-hide_banner -version", 5 seconds each, exit code 0 required)
/// and the storage directory (set, absolute, exists, a ".write-test-&lt;guid&gt;" file can be created then
/// deleted). Program.cs runs the probe before the migrations; outside Development an unavailable audio
/// setup stops the API there. Messages and logs never contain a path value.
/// </summary>
public sealed class AudioAvailability(IOptions<AudioOptions> options, ILogger<AudioAvailability> logger)
    : IAudioAvailability
{
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(5);
    private static readonly string[] VersionArguments = ["-hide_banner", "-version"];
    private const int VersionOutputCap = 64 * 1024;

    private volatile bool _ffmpegOk;
    private volatile bool _ffprobeOk;
    private volatile bool _storageOk;
    private volatile string? _failureMessage = "Audio is not available: the audio probe has not run.";

    /// <inheritdoc />
    public bool IsAvailable => _ffmpegOk && _ffprobeOk && _storageOk;

    /// <inheritdoc />
    public bool FfmpegOk => _ffmpegOk;

    /// <inheritdoc />
    public bool FfprobeOk => _ffprobeOk;

    /// <inheritdoc />
    public bool StorageOk => _storageOk;

    /// <inheritdoc />
    public string? FailureMessage => _failureMessage;

    /// <inheritdoc />
    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        var audio = options.Value;
        var ffmpegOk = await ProbeToolAsync("ffmpeg", audio.FfmpegPath, cancellationToken);
        var ffprobeOk = await ProbeToolAsync("ffprobe", audio.FfprobePath, cancellationToken);
        var storageOk = await ProbeStorageAsync(audio.StoragePath, cancellationToken);

        var failures = new List<string>();
        if (!ffmpegOk)
            failures.Add("ffmpeg did not run (Audio:FfmpegPath)");
        if (!ffprobeOk)
            failures.Add("ffprobe did not run (Audio:FfprobePath)");
        if (!storageOk)
            failures.Add("storage is not set, not absolute, missing or not writable (Audio:StoragePath)");

        _ffmpegOk = ffmpegOk;
        _ffprobeOk = ffprobeOk;
        _storageOk = storageOk;
        _failureMessage = failures.Count == 0
            ? null
            : "Audio is not available: " + string.Join("; ", failures) + ".";
    }

    private async Task<bool> ProbeToolAsync(string tool, string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ToolTimeout);
        try
        {
            var result = await AudioProcessRunner.RunAsync(path, VersionArguments, VersionOutputCap, timeout.Token);
            return result.Started && result.ExitCode == 0 && !result.StdoutCapped;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("[AUDIO] probe of {Tool} timed out after {Seconds}s", tool, (int)ToolTimeout.TotalSeconds);
            return false;
        }
    }

    private static async Task<bool> ProbeStorageAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !Directory.Exists(path))
            return false;

        var testFile = Path.Combine(path, $".write-test-{Guid.NewGuid():D}");
        try
        {
            await File.WriteAllBytesAsync(testFile, [0], cancellationToken);
            File.Delete(testFile);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Best effort.
            }
            return false;
        }
    }
}
