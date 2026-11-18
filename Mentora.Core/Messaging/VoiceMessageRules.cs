namespace Mentora.Core.Messaging;

/// <summary>
/// Lot 13.2: shared rules of voice messages. The limits are enforced by the service from 13.3
/// (upload) and 13.4 (listening), not by the database CHECK constraints.
/// </summary>
public static class VoiceMessageRules
{
    /// <summary>Maximum size of an uploaded audio file, in bytes (5 MB).</summary>
    public const int MaxSizeBytes = 5 * 1024 * 1024;

    /// <summary>Minimum duration of a voice message, in milliseconds.</summary>
    public const int MinDurationMs = 300;

    /// <summary>Maximum duration of a voice message, in milliseconds.</summary>
    public const int MaxDurationMs = 120_000;

    /// <summary>Number of bars of the stored waveform.</summary>
    public const int WaveformBars = 48;

    /// <summary>Maximum value of a waveform bar (bars range from 0 to this value).</summary>
    public const int WaveformMax = 100;

    /// <summary>
    /// Lot 13.4: maximum size of the whole multipart upload request, in bytes (8 MB). A file between
    /// <see cref="MaxSizeBytes"/> and this value reaches the service and gets a 422; a bigger body is
    /// refused by the server with 413.
    /// </summary>
    public const int MaxRequestBytes = 8 * 1024 * 1024;

    /// <summary>Lot 13.4: voice uploads allowed per authenticated user and per window.</summary>
    public const int VoiceUploadPermitLimit = 30;

    /// <summary>Lot 13.4: length of the voice upload rate limit window, in minutes.</summary>
    public const int VoiceUploadWindowMinutes = 10;

    /// <summary>Content type of an AAC ADTS audio file.</summary>
    public const string ContentTypeAdts = "audio/aac";

    /// <summary>Content type of an MP4 (M4A) audio file.</summary>
    public const string ContentTypeMp4 = "audio/mp4";

    /// <summary>
    /// Relative URL of the listening route of a voice message:
    /// /api/v1/conversations/{conversationId}/messages/{messageId}/audio (lowercase GUIDs, "D" format).
    /// </summary>
    public static string MediaUrl(Guid conversationId, Guid messageId)
        => $"/api/v1/conversations/{conversationId.ToString("D")}/messages/{messageId.ToString("D")}/audio";

    /// <summary>
    /// Duration exposed on the wire, in whole seconds: rounded half away from zero, never below 1.
    /// </summary>
    public static int DurationSeconds(int durationMs)
        => Math.Max(1, (int)Math.Round(durationMs / 1000.0, MidpointRounding.AwayFromZero));
}
