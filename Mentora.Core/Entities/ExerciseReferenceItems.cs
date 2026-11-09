namespace Mentora.Core.Entities;

// Lot 12.2a — closed reference lists of the exercise catalog, one table per list.
// The code is the stable identifier, the label is what users see, the sort order drives display.
// Rows are created by ExerciseReferenceSeeder from EXERCISE_REFERENCES.csv when their code is
// missing; existing rows are never updated nor deleted by code. CHECK constraints
// CK_<TABLE>_CODE_FORMAT and CK_<TABLE>_SORT_ORDER live in the Lot12_2a migration.

/// <summary>Table EXERCISE_CATEGORIES.</summary>
public class ExerciseCategoryItem
{
    public Guid ExerciseCategoryId { get; set; }
    public string ExerciseCategoryCode { get; set; } = null!;
    public string ExerciseCategoryLabel { get; set; } = null!;
    public int ExerciseCategorySortOrder { get; set; }
    public bool ExerciseCategoryIsActive { get; set; } = true;
    public DateTime ExerciseCategoryCreatedDate { get; set; }
    public DateTime ExerciseCategoryUpdatedDate { get; set; }
}

/// <summary>Table EXERCISE_TYPES.</summary>
public class ExerciseTypeItem
{
    public Guid ExerciseTypeId { get; set; }
    public string ExerciseTypeCode { get; set; } = null!;
    public string ExerciseTypeLabel { get; set; } = null!;
    public int ExerciseTypeSortOrder { get; set; }
    public bool ExerciseTypeIsActive { get; set; } = true;
    public DateTime ExerciseTypeCreatedDate { get; set; }
    public DateTime ExerciseTypeUpdatedDate { get; set; }
}

/// <summary>Table EXERCISE_LATERALITIES.</summary>
public class ExerciseLateralityItem
{
    public Guid ExerciseLateralityId { get; set; }
    public string ExerciseLateralityCode { get; set; } = null!;
    public string ExerciseLateralityLabel { get; set; } = null!;
    public int ExerciseLateralitySortOrder { get; set; }
    public bool ExerciseLateralityIsActive { get; set; } = true;
    public DateTime ExerciseLateralityCreatedDate { get; set; }
    public DateTime ExerciseLateralityUpdatedDate { get; set; }
}

/// <summary>Table MOVEMENT_PATTERNS.</summary>
public class MovementPatternItem
{
    public Guid MovementPatternId { get; set; }
    public string MovementPatternCode { get; set; } = null!;
    public string MovementPatternLabel { get; set; } = null!;
    public int MovementPatternSortOrder { get; set; }
    public bool MovementPatternIsActive { get; set; } = true;
    public DateTime MovementPatternCreatedDate { get; set; }
    public DateTime MovementPatternUpdatedDate { get; set; }
}

/// <summary>Table STARTING_POSITIONS.</summary>
public class StartingPositionItem
{
    public Guid StartingPositionId { get; set; }
    public string StartingPositionCode { get; set; } = null!;
    public string StartingPositionLabel { get; set; } = null!;
    public int StartingPositionSortOrder { get; set; }
    public bool StartingPositionIsActive { get; set; } = true;
    public DateTime StartingPositionCreatedDate { get; set; }
    public DateTime StartingPositionUpdatedDate { get; set; }
}

/// <summary>Table GRIP_TYPES.</summary>
public class GripTypeItem
{
    public Guid GripTypeId { get; set; }
    public string GripTypeCode { get; set; } = null!;
    public string GripTypeLabel { get; set; } = null!;
    public int GripTypeSortOrder { get; set; }
    public bool GripTypeIsActive { get; set; } = true;
    public DateTime GripTypeCreatedDate { get; set; }
    public DateTime GripTypeUpdatedDate { get; set; }
}
