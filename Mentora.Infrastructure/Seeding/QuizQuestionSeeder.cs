using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Mentora.Core.Entities;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

/// <summary>
/// Imports the quiz questions from the QUESTIONS.csv export of the quiz sheet (embedded resource,
/// see Mentora.Infrastructure.csproj). Runs after the lot 9 seeders.
///
/// Idempotent row by row, matched on the source code ("ID question"): a missing code creates the
/// question, an existing one only gets its NULL or empty columns filled. A non-empty value is never
/// overwritten and IS_ACTIVE is never touched. Every row is imported whatever its "Statut" (kept in
/// SOURCE_STATUS). Columns are read by header name, never by position.
///
/// Skipped rows (never truncated, never partially imported): placeholder rows (every column but
/// "Statut" empty, one aggregated log line), empty code, code duplicated in the file (all its rows),
/// empty question or answer, correct answer outside A-D, XP not an integer >= 0, value longer than
/// its column. A missing resource or header throws at startup.
/// </summary>
public static class QuizQuestionSeeder
{
    private const string Tag = "[QUIZ]";
    private const string ResourceName = "Mentora.Quiz.QUESTIONS.csv";

    private const string CodeHeader = "ID question";
    private const string StatusHeader = "Statut";

    private static readonly string[] Headers =
    [
        "ID question", "ID matière", "Matière", "ID chapitre", "Nom du chapitre", "ID fiche",
        "Titre de la fiche", "Question", "Réponse A", "Réponse B", "Réponse C", "Réponse D",
        "Bonne réponse", "Explication", "Difficulté", "XP", "Statut",
    ];

    // Column limits from QuizQuestionConfiguration.
    private const int CodeMax = 20;
    private const int SubjectMax = 200;
    private const int ChapterMax = 200;
    private const int CardTitleMax = 300;
    private const int DifficultyMax = 50;
    private const int StatusMax = 50;

    private sealed record CsvRow(int Line, Dictionary<string, string> Values);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db     = services.GetRequiredService<MentoraDbContext>();
        var logger = services.GetRequiredService<ILogger<MentoraDbContext>>();

        // Read everything before touching the database: a missing resource or header throws here.
        var rows = ReadCsv();
        var now  = DateTime.UtcNow;

        var warnings = new List<string>();
        int created = 0, existing = 0, filled = 0, skipped = 0;

        // Placeholder rows: nothing but "Statut". One aggregated line, each row counts as a warning.
        var placeholders = rows.Where(IsPlaceholder).ToList();
        var candidates   = rows.Where(r => !IsPlaceholder(r)).ToList();
        skipped += placeholders.Count;

        var duplicateCodes = candidates
            .GroupBy(r => Clean(r.Values[CodeHeader]) ?? "", StringComparer.Ordinal)
            .Where(g => g.Key != "" && g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Line).ToList(), StringComparer.Ordinal);

        var bySource = (await db.QuizQuestions.ToListAsync())
            .ToDictionary(q => q.QuizQuestionSourceCode, StringComparer.Ordinal);

        foreach (var (code, lines) in duplicateCodes)
        {
            skipped += lines.Count;
            warnings.Add($"question {code}: code used by {lines.Count} rows (CSV lines {string.Join(", ", lines)}), all skipped");
        }

        foreach (var row in candidates)
        {
            var code = Clean(row.Values[CodeHeader]);
            if (code is null)
            {
                skipped++;
                warnings.Add($"CSV line {row.Line}: empty ID question, skipped");
                continue;
            }
            if (duplicateCodes.ContainsKey(code))
                continue;

            var problem = Validate(row, out var points);
            if (problem != null)
            {
                skipped++;
                warnings.Add($"question {code} (CSV line {row.Line}): {problem}, skipped");
                continue;
            }

            if (bySource.TryGetValue(code, out var current))
            {
                existing++;
                if (FillEmpty(current, row, points))
                {
                    current.QuizQuestionUpdatedDate = now;
                    filled++;
                }
                continue;
            }

            var question = new QuizQuestion
            {
                QuizQuestionSourceCode    = code,
                QuizQuestionSubjectCode   = Value(row, "ID matière"),
                QuizQuestionSubject       = Value(row, "Matière"),
                QuizQuestionChapterCode   = Value(row, "ID chapitre"),
                QuizQuestionChapter       = Value(row, "Nom du chapitre"),
                QuizQuestionCardCode      = Value(row, "ID fiche"),
                QuizQuestionCardTitle     = Value(row, "Titre de la fiche"),
                QuizQuestionText          = Value(row, "Question")!,
                QuizQuestionAnswerA       = Value(row, "Réponse A")!,
                QuizQuestionAnswerB       = Value(row, "Réponse B")!,
                QuizQuestionAnswerC       = Value(row, "Réponse C")!,
                QuizQuestionAnswerD       = Value(row, "Réponse D")!,
                QuizQuestionCorrectAnswer = Value(row, "Bonne réponse")!,
                QuizQuestionExplanation   = Value(row, "Explication"),
                QuizQuestionDifficulty    = Value(row, "Difficulté"),
                QuizQuestionPoints        = points,
                QuizQuestionSourceStatus  = Value(row, StatusHeader),
                QuizQuestionIsActive      = true,
                QuizQuestionCreatedDate   = now,
                QuizQuestionUpdatedDate   = now,
            };
            db.QuizQuestions.Add(question);
            bySource[code] = question;
            created++;
        }

        if (created > 0 || filled > 0)
            await db.SaveChangesAsync();

        var warningCount = placeholders.Count + warnings.Count;
        logger.LogInformation(
            "QuizQuestionSeeder: created {Created}, existing {Existing}, filled {Filled}, skipped {Skipped}, warnings {Warnings}.",
            created, existing, filled, skipped, warningCount);

        if (placeholders.Count > 0)
            logger.LogWarning("{Tag} {Count} placeholder rows (only Statut filled) skipped, CSV lines: {Lines}",
                Tag, placeholders.Count, string.Join(", ", placeholders.Select(r => r.Line)));
        foreach (var line in warnings)
            logger.LogWarning("{Tag} {Line}", Tag, line);
    }

    // ── Validation and fill ─────────────────────────────────────────────────────

    /// <summary>Returns the reason a row cannot be imported, or NULL. Parses XP into <paramref name="points"/>.</summary>
    private static string? Validate(CsvRow row, out int points)
    {
        points = 0;
        foreach (var header in new[] { "Question", "Réponse A", "Réponse B", "Réponse C", "Réponse D" })
            if (Value(row, header) is null)
                return $"empty '{header}'";

        var correct = Value(row, "Bonne réponse");
        if (correct is not ("A" or "B" or "C" or "D"))
            return $"correct answer '{correct}' outside A-D";

        var xp = Value(row, "XP");
        if (xp is null || !int.TryParse(xp, NumberStyles.None, CultureInfo.InvariantCulture, out points))
            return $"XP '{xp}' is not an integer >= 0";

        var tooLong = new List<string>();
        CheckLength(tooLong, CodeHeader, Value(row, CodeHeader), CodeMax);
        CheckLength(tooLong, "ID matière", Value(row, "ID matière"), CodeMax);
        CheckLength(tooLong, "Matière", Value(row, "Matière"), SubjectMax);
        CheckLength(tooLong, "ID chapitre", Value(row, "ID chapitre"), CodeMax);
        CheckLength(tooLong, "Nom du chapitre", Value(row, "Nom du chapitre"), ChapterMax);
        CheckLength(tooLong, "ID fiche", Value(row, "ID fiche"), CodeMax);
        CheckLength(tooLong, "Titre de la fiche", Value(row, "Titre de la fiche"), CardTitleMax);
        CheckLength(tooLong, "Difficulté", Value(row, "Difficulté"), DifficultyMax);
        CheckLength(tooLong, StatusHeader, Value(row, StatusHeader), StatusMax);
        if (tooLong.Count > 0)
            return $"too long for its column ({string.Join(", ", tooLong)}), never truncated";

        return null;
    }

    // Fills NULL or empty columns only, returns whether at least one value was written.
    // POINTS (NOT NULL, no "empty" state), IS_ACTIVE and every non-empty value are never touched.
    private static bool FillEmpty(QuizQuestion target, CsvRow row, int points)
    {
        var changed = false;
        void Fill(string? current, string? candidate, Action<string> assign)
        {
            if (string.IsNullOrWhiteSpace(current) && candidate is not null) { assign(candidate); changed = true; }
        }

        Fill(target.QuizQuestionSubjectCode, Value(row, "ID matière"), v => target.QuizQuestionSubjectCode = v);
        Fill(target.QuizQuestionSubject, Value(row, "Matière"), v => target.QuizQuestionSubject = v);
        Fill(target.QuizQuestionChapterCode, Value(row, "ID chapitre"), v => target.QuizQuestionChapterCode = v);
        Fill(target.QuizQuestionChapter, Value(row, "Nom du chapitre"), v => target.QuizQuestionChapter = v);
        Fill(target.QuizQuestionCardCode, Value(row, "ID fiche"), v => target.QuizQuestionCardCode = v);
        Fill(target.QuizQuestionCardTitle, Value(row, "Titre de la fiche"), v => target.QuizQuestionCardTitle = v);
        Fill(target.QuizQuestionText, Value(row, "Question"), v => target.QuizQuestionText = v);
        Fill(target.QuizQuestionAnswerA, Value(row, "Réponse A"), v => target.QuizQuestionAnswerA = v);
        Fill(target.QuizQuestionAnswerB, Value(row, "Réponse B"), v => target.QuizQuestionAnswerB = v);
        Fill(target.QuizQuestionAnswerC, Value(row, "Réponse C"), v => target.QuizQuestionAnswerC = v);
        Fill(target.QuizQuestionAnswerD, Value(row, "Réponse D"), v => target.QuizQuestionAnswerD = v);
        Fill(target.QuizQuestionCorrectAnswer, Value(row, "Bonne réponse"), v => target.QuizQuestionCorrectAnswer = v);
        Fill(target.QuizQuestionExplanation, Value(row, "Explication"), v => target.QuizQuestionExplanation = v);
        Fill(target.QuizQuestionDifficulty, Value(row, "Difficulté"), v => target.QuizQuestionDifficulty = v);
        Fill(target.QuizQuestionSourceStatus, Value(row, StatusHeader), v => target.QuizQuestionSourceStatus = v);
        return changed;
    }

    // ── CSV and text helpers ────────────────────────────────────────────────────

    private static bool IsPlaceholder(CsvRow row) =>
        Headers.Where(h => h != StatusHeader).All(h => Value(row, h) is null);

    private static string? Value(CsvRow row, string header) => Clean(row.Values[header]);

    private static List<CsvRow> ReadCsv()
    {
        using var stream = typeof(QuizQuestionSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Quiz resource '{ResourceName}' is not embedded in Mentora.Infrastructure.");
        // Strict UTF-8 (BOM or not): an invalid byte throws instead of producing replacement characters.
        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            DetectColumnCountChanges = true,
        });

        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
            throw new InvalidOperationException("Quiz resource 'QUESTIONS.csv' has no header row.");
        var headers = csv.HeaderRecord;
        var missing = Headers.Except(headers, StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Quiz resource 'QUESTIONS.csv' is missing header(s): {string.Join(", ", missing)}.");

        var rows = new List<CsvRow>();
        var previousEnd = csv.Parser.RawRow;
        while (csv.Read())
        {
            // First physical line of the record (a quoted field may span several lines).
            var line = previousEnd + 1;
            previousEnd = csv.Parser.RawRow;

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var header in headers)
                values[header] = csv.GetField(header) ?? "";
            rows.Add(new CsvRow(line, values));
        }
        return rows;
    }

    /// <summary>Trimmed, NFC-normalized value (line breaks kept); NULL when empty.</summary>
    private static string? Clean(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Normalize(NormalizationForm.FormC).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static void CheckLength(List<string> tooLong, string header, string? value, int max)
    {
        if (value?.Length > max)
            tooLong.Add($"{header} {value.Length}>{max}");
    }
}
