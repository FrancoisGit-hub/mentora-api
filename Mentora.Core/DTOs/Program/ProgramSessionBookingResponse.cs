namespace Mentora.Core.DTOs.Program;

/// <param name="ProgramSessionId">Unique identifier of the program session.</param>
/// <param name="SessionId">The linked booking, or null when detached.</param>
/// <param name="Name">Name of the program session.</param>
/// <param name="Type">Session type (must match the linked booking's OfferType when linked).</param>
/// <param name="Status">PLANNED, DONE, or SKIPPED.</param>
public record ProgramSessionBookingResponse(
    Guid ProgramSessionId,
    Guid? SessionId,
    string Name,
    string Type,
    string Status);
