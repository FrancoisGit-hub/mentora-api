namespace Mentora.Core.Entities;

/// <summary>
/// Lot 12.2a — marker row of a one-time data patch (table DATA_PATCHES). A patch runs inside one
/// transaction together with the insertion of its marker, so it applies at most once per database.
/// </summary>
public class DataPatch
{
    public string DataPatchName { get; set; } = null!;
    public DateTime DataPatchAppliedDate { get; set; }
}
