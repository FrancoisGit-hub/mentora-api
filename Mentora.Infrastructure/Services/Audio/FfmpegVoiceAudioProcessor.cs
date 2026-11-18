using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Messaging;
using Mentora.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services.Audio;

/// <summary>
/// Lot 13.3. Analyzes a voice message file with ffprobe and ffmpeg (13.C, 13.D, 13.F):
/// magic bytes (ADTS or M4A, nothing else), then under a concurrency gate and one timeout covering both
/// processes: ffprobe (first audio stream must be AAC, container must match the magic bytes), ffmpeg
/// decode to mono 8 kHz 16 bit PCM (capped), duration from the sample count, 48 bar RMS waveform
/// normalized to 0..100. One log line per analysis; user content is never logged.
/// Both processes get "-protocol_whitelist file" and a demuxer forced from the magic bytes ("-f aac" or
/// "-f mov") before the input, so user content is never sniffed into another demuxer and only local files are read.
/// </summary>
public sealed class FfmpegVoiceAudioProcessor : IVoiceAudioProcessor, IDisposable
{
    private const int SampleRate = 8000;
    private const int BytesPerSample = 2;
    private const int ProbeOutputCap = 64 * 1024;
    private const int MagicLength = 12;
    private const int LoggedStderrChars = 200;

    /// <summary>Decode limit in seconds passed to ffmpeg "-t": the maximum duration plus one second.</summary>
    private static readonly int DecodeSecondsCap = VoiceMessageRules.MaxDurationMs / 1000 + 1;

    /// <summary>Largest PCM output read from ffmpeg, in bytes.</summary>
    private static readonly int PcmByteCap = DecodeSecondsCap * SampleRate * BytesPerSample;

    private readonly AudioOptions _options;
    private readonly IAudioAvailability _availability;
    private readonly ILogger<FfmpegVoiceAudioProcessor> _logger;
    private readonly SemaphoreSlim _gate;

    private enum Container { Unknown, Adts, M4a }

    /// <summary>Creates the processor; the concurrency gate holds Audio:MaxConcurrentDecodes slots.</summary>
    public FfmpegVoiceAudioProcessor(
        IOptions<AudioOptions> options,
        IAudioAvailability availability,
        ILogger<FfmpegVoiceAudioProcessor> logger)
    {
        _options = options.Value;
        _availability = availability;
        _logger = logger;
        _gate = new SemaphoreSlim(_options.MaxConcurrentDecodes, _options.MaxConcurrentDecodes);
    }

    /// <inheritdoc />
    public async Task<AudioAnalysisResult> AnalyzeAsync(string filePath, CancellationToken ct)
    {
        if (!_availability.IsAvailable)
            throw new AudioNotConfiguredException();

        var stopwatch = Stopwatch.StartNew();

        var container = await DetectContainerAsync(filePath, ct);
        if (container == Container.Unknown)
            return Finish(AudioAnalysisResult.Failed(AudioAnalysisOutcome.UnsupportedFormat), stopwatch, null, "");

        // A request waits its turn for a slot (no extra timeout); the decode timeout starts once inside.
        await _gate.WaitAsync(ct);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.DecodeTimeoutSeconds));
            try
            {
                return await AnalyzeInsideGateAsync(filePath, container, stopwatch, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("[AUDIO] analysis timeout after {TimeoutSeconds}s, process tree killed",
                    _options.DecodeTimeoutSeconds);
                return Finish(AudioAnalysisResult.Failed(AudioAnalysisOutcome.Undecodable), stopwatch, null, "timeout");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AudioAnalysisResult> AnalyzeInsideGateAsync(
        string filePath, Container container, Stopwatch stopwatch, CancellationToken ct)
    {
        // Input options before the input: local files only, demuxer forced from the magic bytes.
        var demuxer = container == Container.Adts ? "aac" : "mov";

        // Step 3: ffprobe.
        var probe = await AudioProcessRunner.RunAsync(
            _options.FfprobePath,
            ["-v", "error", "-protocol_whitelist", "file", "-f", demuxer,
             "-show_entries", "stream=codec_type,codec_name:format=format_name", "-of", "json", filePath],
            ProbeOutputCap,
            ct);
        if (!probe.Started || probe.StdoutCapped || probe.ExitCode != 0)
            return Finish(AudioAnalysisResult.Failed(AudioAnalysisOutcome.Undecodable), stopwatch, probe.ExitCode, probe.Stderr);

        bool formatOk;
        try
        {
            formatOk = ProbeMatches(probe.Stdout.AsSpan(0, probe.StdoutLength), container);
        }
        catch (JsonException)
        {
            return Finish(AudioAnalysisResult.Failed(AudioAnalysisOutcome.Undecodable), stopwatch, probe.ExitCode, probe.Stderr);
        }
        if (!formatOk)
            return Finish(AudioAnalysisResult.Failed(AudioAnalysisOutcome.UnsupportedFormat), stopwatch, probe.ExitCode, probe.Stderr);

        // Step 4: decode the first audio stream to mono 8 kHz signed 16 bit little endian PCM on stdout.
        var decode = await AudioProcessRunner.RunAsync(
            _options.FfmpegPath,
            ["-v", "error", "-nostdin", "-protocol_whitelist", "file", "-f", demuxer, "-i", filePath, "-map", "0:a:0", "-ac", "1", "-ar", SampleRate.ToString(),
             "-t", DecodeSecondsCap.ToString(), "-f", "s16le", "pipe:1"],
            PcmByteCap,
            ct);
        if (decode.StdoutCapped)
        {
            // More PCM than the decode limit allows: longer than the maximum duration by construction.
            return Finish(AudioAnalysisResult.Failed(AudioAnalysisOutcome.TooLong), stopwatch, decode.ExitCode, decode.Stderr);
        }
        if (!decode.Started || decode.ExitCode != 0)
            return Finish(AudioAnalysisResult.Failed(AudioAnalysisOutcome.Undecodable), stopwatch, decode.ExitCode, decode.Stderr);

        var samples = decode.StdoutLength / BytesPerSample;
        var durationMs = (int)((long)samples * 1000 / SampleRate);
        if (durationMs < VoiceMessageRules.MinDurationMs)
            return Finish(AudioAnalysisResult.Failed(AudioAnalysisOutcome.TooShort), stopwatch, decode.ExitCode, decode.Stderr, durationMs);
        if (durationMs > VoiceMessageRules.MaxDurationMs)
            return Finish(AudioAnalysisResult.Failed(AudioAnalysisOutcome.TooLong), stopwatch, decode.ExitCode, decode.Stderr, durationMs);

        // Step 5: waveform.
        var waveform = ComputeWaveform(decode.Stdout, samples);
        var contentType = container == Container.Adts
            ? VoiceMessageRules.ContentTypeAdts
            : VoiceMessageRules.ContentTypeMp4;
        return Finish(AudioAnalysisResult.Ok(contentType, durationMs, waveform), stopwatch, decode.ExitCode, "");
    }

    /// <summary>Step 1: ADTS when byte0 is 0xFF and (byte1 &amp; 0xF6) is 0xF0; M4A when bytes 4 to 7 are "ftyp".</summary>
    private static async Task<Container> DetectContainerAsync(string filePath, CancellationToken ct)
    {
        var header = new byte[MagicLength];
        var read = 0;
        await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                         bufferSize: 4096, FileOptions.Asynchronous))
        {
            while (read < MagicLength)
            {
                var n = await stream.ReadAsync(header.AsMemory(read, MagicLength - read), ct);
                if (n == 0)
                    break;
                read += n;
            }
        }

        if (read >= 2 && header[0] == 0xFF && (header[1] & 0xF6) == 0xF0)
            return Container.Adts;
        if (read >= 8 && header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p')
            return Container.M4a;
        return Container.Unknown;
    }

    /// <summary>
    /// Step 3 rules: the first audio stream must be AAC; format_name must be "aac" for ADTS, or a list
    /// containing "mp4" or "m4a" for M4A. Throws <see cref="JsonException"/> on unparsable output.
    /// </summary>
    private static bool ProbeMatches(ReadOnlySpan<byte> json, Container container)
    {
        var reader = new Utf8JsonReader(json);
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("ffprobe output is not a JSON object.");

        string? firstAudioCodec = null;
        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streams.EnumerateArray())
            {
                if (stream.ValueKind == JsonValueKind.Object
                    && stream.TryGetProperty("codec_type", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && type.GetString() == "audio")
                {
                    firstAudioCodec = stream.TryGetProperty("codec_name", out var codec) && codec.ValueKind == JsonValueKind.String
                        ? codec.GetString()
                        : "";
                    break;
                }
            }
        }
        if (firstAudioCodec != "aac")
            return false;

        if (!root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.Object
            || !format.TryGetProperty("format_name", out var formatName) || formatName.ValueKind != JsonValueKind.String)
            return false;

        var names = (formatName.GetString() ?? "").Split(',', StringSplitOptions.TrimEntries);
        if (container == Container.Adts)
            return names.Length == 1 && names[0] == "aac";
        if (container == Container.M4a)
            return names.Contains("mp4") || names.Contains("m4a");
        return false;
    }

    /// <summary>
    /// Step 5 (13.F): WaveformBars contiguous slices, slice i = samples [i*n/bars, (i+1)*n/bars); RMS per
    /// slice; value = max == 0 ? 0 : round(rms / max * WaveformMax, half away from zero), clamped.
    /// </summary>
    private static int[] ComputeWaveform(byte[] pcm, int sampleCount)
    {
        const int bars = VoiceMessageRules.WaveformBars;
        var rms = new double[bars];
        var max = 0.0;
        for (var i = 0; i < bars; i++)
        {
            var start = (int)((long)i * sampleCount / bars);
            var end = (int)((long)(i + 1) * sampleCount / bars);
            var sum = 0.0;
            for (var s = start; s < end; s++)
            {
                double value = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(s * BytesPerSample, BytesPerSample));
                sum += value * value;
            }
            rms[i] = end > start ? Math.Sqrt(sum / (end - start)) : 0.0;
            if (rms[i] > max)
                max = rms[i];
        }

        var waveform = new int[bars];
        for (var i = 0; i < bars; i++)
        {
            waveform[i] = max == 0.0
                ? 0
                : Math.Clamp(
                    (int)Math.Round(rms[i] / max * VoiceMessageRules.WaveformMax, MidpointRounding.AwayFromZero),
                    0,
                    VoiceMessageRules.WaveformMax);
        }
        return waveform;
    }

    private AudioAnalysisResult Finish(
        AudioAnalysisResult result, Stopwatch stopwatch, int? exitCode, string stderr, int? measuredDurationMs = null)
    {
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        if (result.Outcome == AudioAnalysisOutcome.Ok)
        {
            _logger.LogInformation("[AUDIO] analysis outcome={Outcome} durationMs={DurationMs} elapsedMs={ElapsedMs}",
                result.Outcome, result.DurationMs, elapsedMs);
        }
        else
        {
            var stderrHead = stderr.Length > LoggedStderrChars ? stderr[..LoggedStderrChars] : stderr;
            stderrHead = stderrHead.Replace('\r', ' ').Replace('\n', ' ');
            _logger.LogInformation(
                "[AUDIO] analysis outcome={Outcome} durationMs={DurationMs} elapsedMs={ElapsedMs} exitCode={ExitCode} stderr={Stderr}",
                result.Outcome, measuredDurationMs, elapsedMs, exitCode, stderrHead);
        }
        return result;
    }

    /// <summary>Releases the concurrency gate.</summary>
    public void Dispose() => _gate.Dispose();
}
