using System.Collections.ObjectModel;

namespace Mentora.Core.Enums;

/// <summary>
/// Wire-format (string) ↔ enum helpers shared by DTOs, validators, and service layer.
/// Each nested class exposes Parse, ToWire, and WireValues so validators and services
/// never hard-code string literals independently.
/// </summary>
public static class EnumMappings
{
    public static class OfferTypeMapping
    {
        public static readonly ReadOnlyCollection<string> WireValues =
            new(["VISIO", "PRESENTIEL_SOLO"]);
        // PresentielGroupe is NOT exposed in V1

        public static OfferType Parse(string value)
        {
            if (value == "VISIO")          return OfferType.Visio;
            if (value == "PRESENTIEL_SOLO") return OfferType.PresentielSolo;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown OfferType wire value.");
        }

        public static string ToWire(OfferType value)
        {
            if (value == OfferType.Visio)          return "VISIO";
            if (value == OfferType.PresentielSolo) return "PRESENTIEL_SOLO";
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown OfferType enum value.");
        }
    }

    public static class SportMapping
    {
        public static readonly ReadOnlyCollection<string> WireValues =
            new(["TRAINING", "BOXE", "SALLE", "COURSE"]);

        public static Sport Parse(string value)
        {
            if (value == "TRAINING") return Sport.Training;
            if (value == "BOXE")     return Sport.Boxe;
            if (value == "SALLE")    return Sport.Salle;
            if (value == "COURSE")   return Sport.Course;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Sport wire value.");
        }

        public static string ToWire(Sport value)
        {
            if (value == Sport.Training) return "TRAINING";
            if (value == Sport.Boxe)     return "BOXE";
            if (value == Sport.Salle)    return "SALLE";
            if (value == Sport.Course)   return "COURSE";
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Sport enum value.");
        }
    }

    public static class ProductStatusMapping
    {
        public static readonly ReadOnlyCollection<string> WireValues =
            new(["DRAFT", "PUBLISHED", "ARCHIVED"]);

        public static ProductStatus Parse(string value)
        {
            if (value == "DRAFT")     return ProductStatus.Draft;
            if (value == "PUBLISHED") return ProductStatus.Published;
            if (value == "ARCHIVED")  return ProductStatus.Archived;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown ProductStatus wire value.");
        }

        public static string ToWire(ProductStatus value)
        {
            if (value == ProductStatus.Draft)     return "DRAFT";
            if (value == ProductStatus.Published) return "PUBLISHED";
            if (value == ProductStatus.Archived)  return "ARCHIVED";
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown ProductStatus enum value.");
        }
    }
}
