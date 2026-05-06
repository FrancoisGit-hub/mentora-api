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

    public static class OrderStatusMapping
    {
        public static readonly ReadOnlyCollection<string> WireValues =
            new(["PENDING", "PAID", "EXPIRED", "FAILED"]);

        public static OrderStatus Parse(string value)
        {
            if (value == "PENDING") return OrderStatus.Pending;
            if (value == "PAID")    return OrderStatus.Paid;
            if (value == "EXPIRED") return OrderStatus.Expired;
            if (value == "FAILED")  return OrderStatus.Failed;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown OrderStatus wire value.");
        }

        public static string ToWire(OrderStatus value)
        {
            if (value == OrderStatus.Pending) return "PENDING";
            if (value == OrderStatus.Paid)    return "PAID";
            if (value == OrderStatus.Expired) return "EXPIRED";
            if (value == OrderStatus.Failed)  return "FAILED";
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown OrderStatus enum value.");
        }
    }

    public static class VoucherStatusMapping
    {
        public static readonly ReadOnlyCollection<string> WireValues =
            new(["AVAILABLE", "RESERVED", "CONSUMED"]);

        public static VoucherStatus Parse(string value)
        {
            if (value == "AVAILABLE") return VoucherStatus.Available;
            if (value == "RESERVED")  return VoucherStatus.Reserved;
            if (value == "CONSUMED")  return VoucherStatus.Consumed;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown VoucherStatus wire value.");
        }

        public static string ToWire(VoucherStatus value)
        {
            if (value == VoucherStatus.Available) return "AVAILABLE";
            if (value == VoucherStatus.Reserved)  return "RESERVED";
            if (value == VoucherStatus.Consumed)  return "CONSUMED";
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown VoucherStatus enum value.");
        }
    }

    public static class StripeEventStatusMapping
    {
        public static readonly ReadOnlyCollection<string> WireValues =
            new(["RECEIVED", "PROCESSED", "FAILED"]);

        public static StripeEventStatus Parse(string value)
        {
            if (value == "RECEIVED")  return StripeEventStatus.Received;
            if (value == "PROCESSED") return StripeEventStatus.Processed;
            if (value == "FAILED")    return StripeEventStatus.Failed;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown StripeEventStatus wire value.");
        }

        public static string ToWire(StripeEventStatus value)
        {
            if (value == StripeEventStatus.Received)  return "RECEIVED";
            if (value == StripeEventStatus.Processed) return "PROCESSED";
            if (value == StripeEventStatus.Failed)    return "FAILED";
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown StripeEventStatus enum value.");
        }
    }
}
