using Mentora.Core.Entities;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

/// <summary>An exercise with its search relevance (0 = best).</summary>
internal sealed class RankedExercise
{
    public Exercise Exercise { get; init; } = null!;
    public int Rank { get; init; }
}

/// <summary>
/// Exercise search (member and coach lists). Matches the exercise name and its alternative names
/// (English name, search aliases) only, never a description, instruction or advice, and ranks by
/// relevance, all in SQL.
/// </summary>
internal static class ExerciseSearch
{
    public const int MaxRank = 3;
    private const int NoMatch = 4;

    // Accent folding without the unaccent extension (the schema has none): both the column and the
    // term go through the same translate() + lower(), so "dev", "DÉV" and "dév" are the same term.
    private const string Accented = "àâäÀÂÄéèêëÉÈÊËîïÎÏôöÔÖùûüÙÛÜÿŸçÇ";
    private const string Plain    = "aaaAAAeeeeEEEEiiIIooOOuuuUUUyYcC";

    public static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>
    /// Rank 0: the name starts with the term. 1: a word of the name starts with it (after a space,
    /// hyphen, apostrophe or opening parenthesis). 2: the name contains it. 3: only an alternative
    /// name contains it. 4: no match (callers keep <see cref="MaxRank"/> at most).
    /// </summary>
    public static IQueryable<RankedExercise> WithRank(IQueryable<Exercise> query, string term)
    {
        var escaped = EscapeLike(term.Trim());

        return from e in query
               let t    = MentoraDbFunctions.Translate(escaped, Accented, Plain).ToLower()
               let name = MentoraDbFunctions.Translate(e.ExerciseName, Accented, Plain).ToLower()
               let en   = MentoraDbFunctions.Translate(e.ExerciseNameEn ?? "", Accented, Plain).ToLower()
               let tags = MentoraDbFunctions.Translate(e.ExerciseSearchTags ?? "", Accented, Plain).ToLower()
               select new RankedExercise
               {
                   Exercise = e,
                   Rank = EF.Functions.Like(name, t + "%", "\\") ? 0
                        : (EF.Functions.Like(name, "% " + t + "%", "\\")
                           || EF.Functions.Like(name, "%-" + t + "%", "\\")
                           || EF.Functions.Like(name, "%'" + t + "%", "\\")
                           || EF.Functions.Like(name, "%’" + t + "%", "\\")
                           || EF.Functions.Like(name, "%(" + t + "%", "\\")) ? 1
                        : EF.Functions.Like(name, "%" + t + "%", "\\") ? 2
                        : (EF.Functions.Like(en, "%" + t + "%", "\\")
                           || EF.Functions.Like(tags, "%" + t + "%", "\\")) ? 3
                        : NoMatch,
               };
    }
}
