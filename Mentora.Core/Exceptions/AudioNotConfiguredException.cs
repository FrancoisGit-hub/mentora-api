namespace Mentora.Core.Exceptions;

/// <summary>
/// Lot 13.3. An audio feature was called while ffmpeg, ffprobe or the audio storage is not available
/// (allowed in Development only). GlobalExceptionMiddleware maps it to 503 with error "AUDIO_NOT_CONFIGURED".
/// </summary>
public class AudioNotConfiguredException() : Exception("AUDIO_NOT_CONFIGURED");
