namespace Mentora.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL built-in functions usable in LINQ queries (mapped in <see cref="MentoraDbContext"/>).
/// They exist for translation only and throw when called in memory.
/// </summary>
public static class MentoraDbFunctions
{
    /// <summary>PostgreSQL <c>translate(source, from, to)</c>: replaces each character of <c>from</c> by the one of <c>to</c> at the same position.</summary>
    public static string Translate(string source, string from, string to) =>
        throw new NotSupportedException("Translate can only be used inside a database query.");
}
