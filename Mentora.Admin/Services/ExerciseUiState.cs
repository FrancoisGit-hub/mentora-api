using System.Text.Json;
using Mentora.Admin.Api;

namespace Mentora.Admin.Services;

/// <summary>Petite mémoire de l'écran : dernière adresse de la liste (avec ses filtres) et message à afficher après une navigation.</summary>
public sealed class ExerciseUiState
{
    public string? LastListUrl { get; set; }

    public string? Flash { get; set; }

    public string? TakeFlash()
    {
        var value = Flash;
        Flash = null;
        return value;
    }
}

/// <summary>Textes français et lecture des détails d'erreur de l'écran Exercices.</summary>
public static class ExerciseText
{
    public const string ServerDown = "Le serveur ne répond pas, réessayez.";
    public const string Generic = "Une erreur est survenue, réessayez.";

    // Valeurs envoyées à l'API (jamais le libellé) et libellés affichés.
    public static string LevelLabel(string? wire) => wire switch
    {
        "BEGINNER" => "Débutant",
        "INTERMEDIATE" => "Intermédiaire",
        "ADVANCED" => "Avancé",
        null or "" => "Non renseigné",
        _ => wire
    };

    public static string Failure<T>(ApiResult<T> result) =>
        result.IsNetworkError || result.IsServerError ? ServerDown : Generic;

    public static string InUseMessage(JsonElement? details)
    {
        var text = $"Impossible de désactiver cet exercice : il est utilisé dans {Count(details, "programs")} programme(s), "
                   + $"{Count(details, "programTemplates")} trame(s) et {Count(details, "sessionTemplates")} séance(s) type.";

        var templates = Names(details, "mentoraProgramTemplateNames");
        if (templates.Count > 0)
            text += " Trames Mentora : " + string.Join(", ", templates) + ".";

        var sessions = Names(details, "mentoraSessionTemplateNames");
        if (sessions.Count > 0)
            text += " Séances types Mentora : " + string.Join(", ", sessions) + ".";

        return text;
    }

    public static (Guid Id, string? SourceCode, string? Name)? NameTaken(JsonElement? details)
    {
        if (details is not { ValueKind: JsonValueKind.Object } d)
            return null;
        if (!d.TryGetProperty("exerciseId", out var id) || !Guid.TryParse(id.GetString(), out var guid))
            return null;

        string? Str(string key) =>
            d.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        return (guid, Str("sourceCode"), Str("name"));
    }

    private static int Count(JsonElement? details, string key) =>
        details is { ValueKind: JsonValueKind.Object } d
        && d.TryGetProperty(key, out var v)
        && v.TryGetInt32(out var n)
            ? n
            : 0;

    private static List<string> Names(JsonElement? details, string key)
    {
        var names = new List<string>();
        if (details is { ValueKind: JsonValueKind.Object } d
            && d.TryGetProperty(key, out var v)
            && v.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in v.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s)
                    names.Add(s);
        }
        return names;
    }
}
