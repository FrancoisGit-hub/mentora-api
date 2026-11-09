using System.Text;
using System.Text.Json;

namespace Mentora.Infrastructure.Services;

/// <summary>
/// Limit, search term and cursor handling shared by the member exercise list and the admin exercise list
/// (moved unchanged from ExerciseService in Lot 12.2b). Errors are InvalidOperationException (400).
/// </summary>
internal static class ExerciseListPaging
{
    public const int SearchMinLength = 2;
    public const int SearchMaxLength = 60;

    public static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 100)
            throw new InvalidOperationException($"limit must be between 1 and 100 (got {limit}).");
    }

    /// <summary>Trimmed search term, or null when no search was given; 2 to 60 characters.</summary>
    public static string? NormalizeSearch(string? search)
    {
        if (search is null)
            return null;

        var term = search.Trim();
        if (term.Length is < SearchMinLength or > SearchMaxLength)
            throw new InvalidOperationException(
                $"search must be between {SearchMinLength} and {SearchMaxLength} characters (got {term.Length}).");
        return term;
    }

    public static string EncodeCursor(string name, Guid exerciseId)
    {
        var payload = JsonSerializer.Serialize(new CursorPayload(name, exerciseId));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static CursorPayload DecodeCursor(string cursor)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var payload = JsonSerializer.Deserialize<CursorPayload>(json);
            if (payload is null || payload.Name is null)
                throw new InvalidOperationException("Invalid cursor.");
            return payload;
        }
        catch (FormatException)   { throw new InvalidOperationException("Invalid cursor format."); }
        catch (JsonException)     { throw new InvalidOperationException("Invalid cursor format."); }
        catch (ArgumentException) { throw new InvalidOperationException("Invalid cursor format."); }
    }

    public sealed record CursorPayload(string Name, Guid ExerciseId);

    // Search cursor: the position in the (rank, name, id) order. A cursor of the plain (name, id)
    // format has no rank and is rejected like any cursor of the wrong shape.
    public static string EncodeSearchCursor(int rank, string name, Guid exerciseId)
    {
        var payload = JsonSerializer.Serialize(new SearchCursorPayload(rank, name, exerciseId));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static SearchCursorPayload DecodeSearchCursor(string cursor)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var payload = JsonSerializer.Deserialize<SearchCursorPayload>(json);
            if (payload is null || payload.Name is null || payload.Rank is not (>= 0 and <= ExerciseSearch.MaxRank))
                throw new InvalidOperationException("Invalid cursor.");
            return payload;
        }
        catch (FormatException)   { throw new InvalidOperationException("Invalid cursor format."); }
        catch (JsonException)     { throw new InvalidOperationException("Invalid cursor format."); }
        catch (ArgumentException) { throw new InvalidOperationException("Invalid cursor format."); }
    }

    public sealed record SearchCursorPayload(int? Rank, string Name, Guid ExerciseId);
}
