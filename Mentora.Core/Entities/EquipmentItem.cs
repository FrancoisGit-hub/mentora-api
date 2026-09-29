namespace Mentora.Core.Entities;

/// <summary>
/// Reference row of the exercise catalog equipment (table EQUIPMENT).
/// Named "Item" because the Equipment enum already exists.
/// </summary>
public class EquipmentItem
{
    /// <summary>Name of the equipment row that stands for "no equipment needed".</summary>
    public const string NoEquipmentName = "Sans matériel";

    public Guid EquipmentId { get; set; }
    public string? EquipmentSourceCode { get; set; }
    public string EquipmentName { get; set; } = null!;
    public string? EquipmentCategory { get; set; }
    public string? EquipmentSubcategory { get; set; }

    /// <summary>DB value of the legacy Equipment enum this item maps to (e.g. "BARBELL").</summary>
    public string EquipmentLegacyCode { get; set; } = "OTHER";

    public bool EquipmentIsActive { get; set; } = true;

    public DateTime EquipmentCreatedDate { get; set; }
    public DateTime EquipmentUpdatedDate { get; set; }
}
