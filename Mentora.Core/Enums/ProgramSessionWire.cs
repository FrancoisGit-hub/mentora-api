namespace Mentora.Core.Enums;

/// <summary>Wire values of <c>PROGRAM_SESSION_TYPE</c>. Single source for the validator.</summary>
public static class ProgramSessionTypes
{
    public const string PresentielSolo = "PRESENTIEL_SOLO";
    public const string PresentielGroupe = "PRESENTIEL_GROUPE";
    public const string Visio = "VISIO";

    /// <summary>Session done by the member without the coach; never booked.</summary>
    public const string Autonome = "AUTONOME";

    /// <summary>Stored and returned only for now; the booking link rule comes in Lot 11.2b.</summary>
    public const string PresentielOuVisio = "PRESENTIEL_OU_VISIO";

    public static readonly string[] All =
        [PresentielSolo, PresentielGroupe, Visio, Autonome, PresentielOuVisio];
}

/// <summary>Wire values of <c>PROGRAM_SESSION_NATURE</c>. Single source for the validator.</summary>
public static class ProgramSessionNatures
{
    public const string Standard = "STANDARD";
    public const string Evaluation = "EVALUATION";

    public static readonly string[] All = [Standard, Evaluation];
}
