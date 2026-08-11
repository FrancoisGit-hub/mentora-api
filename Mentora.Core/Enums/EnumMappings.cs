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

    public static class SessionStatusMapping
    {
        public static readonly ReadOnlyCollection<string> WireValues =
            new(["SCHEDULED", "COMPLETED", "CANCELLED"]);

        public static SessionStatus Parse(string value)
        {
            if (value == "SCHEDULED")  return SessionStatus.Scheduled;
            if (value == "COMPLETED")  return SessionStatus.Completed;
            if (value == "CANCELLED")  return SessionStatus.Cancelled;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown SessionStatus wire value.");
        }

        public static string ToWire(SessionStatus value)
        {
            if (value == SessionStatus.Scheduled)  return "SCHEDULED";
            if (value == SessionStatus.Completed)  return "COMPLETED";
            if (value == SessionStatus.Cancelled)  return "CANCELLED";
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown SessionStatus enum value.");
        }
    }

    public static class CancelledByMapping
    {
        public static readonly ReadOnlyCollection<string> WireValues =
            new(["MEMBER", "COACH"]);

        public static CancelledBy Parse(string value)
        {
            if (value == "MEMBER") return CancelledBy.Member;
            if (value == "COACH")  return CancelledBy.Coach;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown CancelledBy wire value.");
        }

        public static string ToWire(CancelledBy value)
        {
            if (value == CancelledBy.Member) return "MEMBER";
            if (value == CancelledBy.Coach)  return "COACH";
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown CancelledBy enum value.");
        }
    }

    public static class SessionStatusFilterMapping
    {
        public static readonly ReadOnlyCollection<string> WireValues =
            new(["SCHEDULED", "COMPLETED", "CANCELLED", "UPCOMING", "PAST"]);

        public static SessionStatusFilter Parse(string value)
        {
            if (value == "SCHEDULED") return SessionStatusFilter.Scheduled;
            if (value == "COMPLETED") return SessionStatusFilter.Completed;
            if (value == "CANCELLED") return SessionStatusFilter.Cancelled;
            if (value == "UPCOMING")  return SessionStatusFilter.Upcoming;
            if (value == "PAST")      return SessionStatusFilter.Past;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown SessionStatusFilter wire value.");
        }

        public static string ToWire(SessionStatusFilter value)
        {
            if (value == SessionStatusFilter.Scheduled) return "SCHEDULED";
            if (value == SessionStatusFilter.Completed) return "COMPLETED";
            if (value == SessionStatusFilter.Cancelled) return "CANCELLED";
            if (value == SessionStatusFilter.Upcoming)  return "UPCOMING";
            if (value == SessionStatusFilter.Past)      return "PAST";
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown SessionStatusFilter enum value.");
        }
    }

    public static class MuscleGroupMapping
    {
        public static readonly ReadOnlyCollection<string> WireValues =
            new([
                "CHEST", "BACK", "SHOULDERS", "BICEPS", "TRICEPS", "FOREARMS", "ABS",
                "QUADRICEPS", "HAMSTRINGS", "GLUTES", "CALVES", "FULL_BODY", "CARDIO"
            ]);

        public static MuscleGroup Parse(string value)
        {
            if (value == "CHEST")      return MuscleGroup.Chest;
            if (value == "BACK")       return MuscleGroup.Back;
            if (value == "SHOULDERS")  return MuscleGroup.Shoulders;
            if (value == "BICEPS")     return MuscleGroup.Biceps;
            if (value == "TRICEPS")    return MuscleGroup.Triceps;
            if (value == "FOREARMS")   return MuscleGroup.Forearms;
            if (value == "ABS")        return MuscleGroup.Abs;
            if (value == "QUADRICEPS") return MuscleGroup.Quadriceps;
            if (value == "HAMSTRINGS") return MuscleGroup.Hamstrings;
            if (value == "GLUTES")     return MuscleGroup.Glutes;
            if (value == "CALVES")     return MuscleGroup.Calves;
            if (value == "FULL_BODY")  return MuscleGroup.FullBody;
            if (value == "CARDIO")     return MuscleGroup.Cardio;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown MuscleGroup wire value.");
        }

        public static string ToWire(MuscleGroup value)
        {
            if (value == MuscleGroup.Chest)      return "CHEST";
            if (value == MuscleGroup.Back)       return "BACK";
            if (value == MuscleGroup.Shoulders)  return "SHOULDERS";
            if (value == MuscleGroup.Biceps)     return "BICEPS";
            if (value == MuscleGroup.Triceps)    return "TRICEPS";
            if (value == MuscleGroup.Forearms)   return "FOREARMS";
            if (value == MuscleGroup.Abs)        return "ABS";
            if (value == MuscleGroup.Quadriceps) return "QUADRICEPS";
            if (value == MuscleGroup.Hamstrings) return "HAMSTRINGS";
            if (value == MuscleGroup.Glutes)     return "GLUTES";
            if (value == MuscleGroup.Calves)     return "CALVES";
            if (value == MuscleGroup.FullBody)   return "FULL_BODY";
            if (value == MuscleGroup.Cardio)     return "CARDIO";
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown MuscleGroup enum value.");
        }
    }

    public static class EquipmentMapping
    {
        public static readonly ReadOnlyCollection<string> WireValues =
            new([
                "BODYWEIGHT", "BARBELL", "DUMBBELL", "KETTLEBELL", "MACHINE", "CABLE",
                "ELASTIC", "OTHER"
            ]);

        public static Equipment Parse(string value)
        {
            if (value == "BODYWEIGHT") return Equipment.Bodyweight;
            if (value == "BARBELL")    return Equipment.Barbell;
            if (value == "DUMBBELL")   return Equipment.Dumbbell;
            if (value == "KETTLEBELL") return Equipment.Kettlebell;
            if (value == "MACHINE")    return Equipment.Machine;
            if (value == "CABLE")      return Equipment.Cable;
            if (value == "ELASTIC")    return Equipment.Elastic;
            if (value == "OTHER")      return Equipment.Other;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Equipment wire value.");
        }

        public static string ToWire(Equipment value)
        {
            if (value == Equipment.Bodyweight) return "BODYWEIGHT";
            if (value == Equipment.Barbell)    return "BARBELL";
            if (value == Equipment.Dumbbell)   return "DUMBBELL";
            if (value == Equipment.Kettlebell) return "KETTLEBELL";
            if (value == Equipment.Machine)    return "MACHINE";
            if (value == Equipment.Cable)      return "CABLE";
            if (value == Equipment.Elastic)    return "ELASTIC";
            if (value == Equipment.Other)      return "OTHER";
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Equipment enum value.");
        }
    }
}
