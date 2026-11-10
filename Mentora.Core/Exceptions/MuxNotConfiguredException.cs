namespace Mentora.Core.Exceptions;

/// <summary>
/// Lot 12.3a — a MUX feature was called while the Mux settings are empty (allowed in Development only).
/// GlobalExceptionMiddleware maps it to 503 with error "MUX_NOT_CONFIGURED".
/// </summary>
public class MuxNotConfiguredException() : Exception("MUX_NOT_CONFIGURED");
