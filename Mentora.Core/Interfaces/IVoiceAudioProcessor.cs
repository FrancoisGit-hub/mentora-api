using Mentora.Core.Messaging;

namespace Mentora.Core.Interfaces;

/// <summary>
/// Lot 13.3. Analyzes an uploaded voice message already written to disk: format check, decoded
/// duration and waveform.
/// </summary>
public interface IVoiceAudioProcessor
{
    /// <summary>
    /// Analyzes the file. Throws <see cref="Mentora.Core.Exceptions.AudioNotConfiguredException"/> when
    /// the audio tooling is not available. A timeout gives <see cref="AudioAnalysisOutcome.Undecodable"/>.
    /// </summary>
    Task<AudioAnalysisResult> AnalyzeAsync(string filePath, CancellationToken ct);
}
