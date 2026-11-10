namespace Mentora.Core.Exceptions;

/// <summary>
/// Lot 12.3a — MUX answered with an unexpected status, timed out or could not be reached.
/// GlobalExceptionMiddleware maps it to 502 with error "MUX_UNAVAILABLE". The message never holds a
/// URL, a body or a credential.
/// </summary>
public class MuxUnavailableException(string message) : Exception(message);
