namespace Mentora.Core.DTOs.Lot2;

public record CoachParameterDto(
    decimal HourlyRateEuros,
    int CancellationDelayHours,
    DateTime UpdatedAt
);
