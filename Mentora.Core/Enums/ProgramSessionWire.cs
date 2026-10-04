namespace Mentora.Core.Enums;

/// <summary>Wire values of <c>PROGRAM_SESSION_TYPE</c>. Single source for the validator.</summary>
public static class ProgramSessionTypes
{
    public const string PresentielSolo = "PRESENTIEL_SOLO";
    public const string PresentielGroupe = "PRESENTIEL_GROUPE";
    public const string Visio = "VISIO";

    /// <summary>Session done by the member without the coach; never booked.</summary>
    public const string Autonome = "AUTONOME";

    /// <summary>Either in person or by visio; compatible with a VISIO or PRESENTIEL_SOLO booking.</summary>
    public const string PresentielOuVisio = "PRESENTIEL_OU_VISIO";

    public static readonly string[] All =
        [PresentielSolo, PresentielGroupe, Visio, Autonome, PresentielOuVisio];

    /// <summary>
    /// Program session types a booking of the given offer type may be linked to. VISIO_GROUPE
    /// matches nothing and AUTONOME is matched by no offer.
    /// </summary>
    public static string[] CompatibleWithOffer(OfferType offerType)
    {
        if (offerType == OfferType.Visio) return [Visio, PresentielOuVisio];
        if (offerType == OfferType.PresentielSolo) return [PresentielSolo, PresentielOuVisio];
        if (offerType == OfferType.PresentielGroupe) return [PresentielGroupe];
        return [];
    }

    public static bool IsCompatibleWithOffer(string programSessionType, OfferType offerType) =>
        CompatibleWithOffer(offerType).Contains(programSessionType);
}

/// <summary>Wire values of <c>PROGRAM_SESSION_NATURE</c>. Single source for the validator.</summary>
public static class ProgramSessionNatures
{
    public const string Standard = "STANDARD";
    public const string Evaluation = "EVALUATION";

    public static readonly string[] All = [Standard, Evaluation];
}
