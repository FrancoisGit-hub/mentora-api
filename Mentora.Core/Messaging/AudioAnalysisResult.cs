namespace Mentora.Core.Messaging;

/// <summary>Lot 13.3. Outcome of the server side analysis of an uploaded voice message.</summary>
public enum AudioAnalysisOutcome
{
    /// <summary>AAC audio decoded, duration within the limits, waveform computed.</summary>
    Ok,

    /// <summary>Not AAC in an ADTS or M4A container (checked on the magic bytes, then by ffprobe).</summary>
    UnsupportedFormat,

    /// <summary>Decoded duration below <see cref="VoiceMessageRules.MinDurationMs"/>.</summary>
    TooShort,

    /// <summary>Decoded duration above <see cref="VoiceMessageRules.MaxDurationMs"/>.</summary>
    TooLong,

    /// <summary>ffprobe or ffmpeg failed, produced unreadable output, or timed out.</summary>
    Undecodable
}

/// <summary>
/// Lot 13.3. Result of <c>IVoiceAudioProcessor.AnalyzeAsync</c>. Only an <see cref="AudioAnalysisOutcome.Ok"/>
/// result carries a content type, a duration and a waveform; every other outcome has them null.
/// </summary>
/// <param name="Outcome">Outcome of the analysis.</param>
/// <param name="ContentType"><see cref="VoiceMessageRules.ContentTypeAdts"/> or <see cref="VoiceMessageRules.ContentTypeMp4"/> when Ok.</param>
/// <param name="DurationMs">Decoded duration in milliseconds when Ok.</param>
/// <param name="Waveform">Exactly <see cref="VoiceMessageRules.WaveformBars"/> values from 0 to <see cref="VoiceMessageRules.WaveformMax"/> when Ok.</param>
public sealed record AudioAnalysisResult(
    AudioAnalysisOutcome Outcome,
    string? ContentType,
    int? DurationMs,
    IReadOnlyList<int>? Waveform)
{
    /// <summary>Successful analysis.</summary>
    public static AudioAnalysisResult Ok(string contentType, int durationMs, IReadOnlyList<int> waveform)
        => new(AudioAnalysisOutcome.Ok, contentType, durationMs, waveform);

    /// <summary>Failed analysis: no content type, duration or waveform.</summary>
    public static AudioAnalysisResult Failed(AudioAnalysisOutcome outcome)
    {
        if (outcome == AudioAnalysisOutcome.Ok)
            throw new ArgumentException("A failed result cannot have the Ok outcome.", nameof(outcome));
        return new(outcome, null, null, null);
    }
}
