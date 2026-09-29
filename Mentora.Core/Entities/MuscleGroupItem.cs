namespace Mentora.Core.Entities;

/// <summary>
/// Reference row of the exercise catalog muscle groups (table MUSCLE_GROUPS).
/// Named "Item" because the MuscleGroup enum already exists.
/// </summary>
public class MuscleGroupItem
{
    public Guid MuscleGroupId { get; set; }
    public string MuscleGroupName { get; set; } = null!;
    public int MuscleGroupSortOrder { get; set; }

    /// <summary>DB value of the legacy MuscleGroup enum this group maps to (e.g. "CHEST").</summary>
    public string MuscleGroupLegacyCode { get; set; } = null!;

    public DateTime MuscleGroupCreatedDate { get; set; }
    public DateTime MuscleGroupUpdatedDate { get; set; }
}
