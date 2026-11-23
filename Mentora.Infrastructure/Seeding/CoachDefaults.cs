namespace Mentora.Infrastructure.Seeding;

/// <summary>
/// Lot 14.8a (14.G9): default values of a new coach, shared by the startup seeders and the admin coach
/// creation so that there is one source of truth.
/// </summary>
internal static class CoachDefaults
{
    public const decimal HourlyRateEuros = 50.00m;
    public const int CancellationDelayHours = 24;

    public static readonly string[] OfferProgramNames =
    [
        "Prise de masse",
        "Perte de poids",
        "Préparation Marathon",
        "Préparation Hyrox"
    ];
}
