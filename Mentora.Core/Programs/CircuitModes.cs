using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.Programs;

/// <summary>
/// Single source of the circuit modes (block types) of the mobile app. The mode stays a string end to end
/// (no enum, no EnumMappings entry). Matching is exact: no case folding.
/// </summary>
public static class CircuitModes
{
    public const string SerieSimple   = "SERIE_SIMPLE";
    public const string Biset         = "BISET";
    public const string Triset        = "TRISET";
    public const string Geante        = "GEANTE";
    public const string Circuit       = "CIRCUIT";
    public const string Amrap         = "AMRAP";
    public const string Emom          = "EMOM";
    public const string ForTime       = "FOR_TIME";
    public const string Interval      = "INTERVAL";
    public const string TempsContinu  = "TEMPS_CONTINU";
    public const string Distance      = "DISTANCE";
    public const string Libre         = "LIBRE";

    /// <summary>Legacy value, still accepted on write and always stored as <see cref="SerieSimple"/>.</summary>
    public const string LegacyStandard = "STANDARD";

    /// <summary>The 12 accepted modes, in display order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        SerieSimple, Biset, Triset, Geante, Circuit, Amrap, Emom, ForTime, Interval, TempsContinu, Distance, Libre,
    ];

    /// <summary>Modes that follow the former STANDARD rules (sets and reps required, work and rest-work forbidden).</summary>
    public static readonly IReadOnlySet<string> StandardLike =
        new HashSet<string> { SerieSimple, Biset, Triset, Geante };

    /// <summary>STANDARD becomes SERIE_SIMPLE, any other value (null included) is returned unchanged.</summary>
    public static string? Normalize(string? mode) => mode == LegacyStandard ? SerieSimple : mode;

    /// <summary>True for one of the 12 modes. The legacy alias must be normalized first.</summary>
    public static bool IsValid(string? mode) => mode is not null && All.Contains(mode);

    /// <summary>Normalizes the mode of every circuit of the list in place (write paths, after validation).</summary>
    public static void NormalizeAll(IEnumerable<ProgramTemplateCircuit>? circuits)
    {
        if (circuits is null)
            return;

        foreach (var circuit in circuits)
        {
            if (circuit is not null)
                circuit.Mode = Normalize(circuit.Mode)!;
        }
    }

    /// <summary>Normalizes the mode of every circuit of a block tree in place (write paths, after validation).</summary>
    public static void NormalizeAll(IEnumerable<ProgramTemplateBlock>? blocks)
    {
        if (blocks is null)
            return;

        foreach (var block in blocks)
        {
            if (block is null)
                continue;

            NormalizeAll(block.Blocks);

            if (block.Sessions is null)
                continue;

            foreach (var session in block.Sessions)
            {
                if (session is not null)
                    NormalizeAll(session.Circuits);
            }
        }
    }
}
