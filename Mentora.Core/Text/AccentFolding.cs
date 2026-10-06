namespace Mentora.Core.Text;

/// <summary>
/// The accent map shared by SQL (<c>translate(col, Accented, Plain)</c> in the exercise search) and
/// C# (<see cref="Fold"/>). A fixed character map, so the result never depends on ICU or on
/// InvariantGlobalization. Ligatures such as œ are not folded (known backlog).
/// </summary>
public static class AccentFolding
{
    public const string Accented = "àâäÀÂÄéèêëÉÈÊËîïÎÏôöÔÖùûüÙÛÜÿŸçÇ";
    public const string Plain    = "aaaAAAeeeeEEEEiiIIooOOuuuUUUyYcC";

    /// <summary>Applies the same map as the SQL translate(), then ToLowerInvariant.</summary>
    public static string Fold(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var index = Accented.IndexOf(chars[i]);
            if (index >= 0) chars[i] = Plain[index];
        }
        return new string(chars).ToLowerInvariant();
    }
}
