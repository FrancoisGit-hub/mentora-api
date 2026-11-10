namespace Mentora.Core.Enums;

/// <summary>Lot 12.2b — what an admin did (ADMIN_AUDIT_LOG_ACTION).</summary>
public enum AdminAuditAction
{
    ExerciseCreated,
    ExerciseUpdated,
    ExerciseActivated,
    ExerciseDeactivated,
    ExerciseVideoUploadCreated, // Lot 12.3a
    ExerciseVideoRemoved,       // Lot 12.3a
}

/// <summary>Lot 12.2b — kind of row an audit entry is about (ADMIN_AUDIT_LOG_ENTITY_TYPE).</summary>
public enum AdminAuditEntityType
{
    Exercise,
}

/// <summary>Database values of the audit enums (if chains, no switch).</summary>
public static class AdminAuditMappings
{
    public static string ActionToDb(AdminAuditAction value)
    {
        if (value == AdminAuditAction.ExerciseCreated)     return "EXERCISE_CREATED";
        if (value == AdminAuditAction.ExerciseUpdated)     return "EXERCISE_UPDATED";
        if (value == AdminAuditAction.ExerciseActivated)   return "EXERCISE_ACTIVATED";
        if (value == AdminAuditAction.ExerciseDeactivated) return "EXERCISE_DEACTIVATED";
        if (value == AdminAuditAction.ExerciseVideoUploadCreated) return "EXERCISE_VIDEO_UPLOAD_CREATED";
        if (value == AdminAuditAction.ExerciseVideoRemoved)       return "EXERCISE_VIDEO_REMOVED";
        throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AdminAuditAction value.");
    }

    public static AdminAuditAction ActionFromDb(string value)
    {
        if (value == "EXERCISE_CREATED")     return AdminAuditAction.ExerciseCreated;
        if (value == "EXERCISE_UPDATED")     return AdminAuditAction.ExerciseUpdated;
        if (value == "EXERCISE_ACTIVATED")   return AdminAuditAction.ExerciseActivated;
        if (value == "EXERCISE_DEACTIVATED") return AdminAuditAction.ExerciseDeactivated;
        if (value == "EXERCISE_VIDEO_UPLOAD_CREATED") return AdminAuditAction.ExerciseVideoUploadCreated;
        if (value == "EXERCISE_VIDEO_REMOVED")        return AdminAuditAction.ExerciseVideoRemoved;
        throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AdminAuditAction DB value.");
    }

    public static string EntityTypeToDb(AdminAuditEntityType value)
    {
        if (value == AdminAuditEntityType.Exercise) return "EXERCISE";
        throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AdminAuditEntityType value.");
    }

    public static AdminAuditEntityType EntityTypeFromDb(string value)
    {
        if (value == "EXERCISE") return AdminAuditEntityType.Exercise;
        throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AdminAuditEntityType DB value.");
    }
}
