using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Mentora.Infrastructure.Services.Mux;

// Lot 12.3a — the subset of the MUX Video REST objects this API reads. Shared by the REST client and the
// webhook (MUX sends the same objects as event "data").

/// <summary>Created direct upload. <see cref="Url"/> is a credential: never logged.</summary>
public sealed record MuxDirectUpload(string Id, string Url, int? TimeoutSeconds);

/// <summary>Direct upload. Status: waiting, asset_created, errored, cancelled, timed_out.</summary>
public sealed record MuxUpload(string Id, string? Status, string? AssetId, string? Passthrough, string? ErrorMessage);

public sealed record MuxPlaybackId(string Id, string? Policy);

/// <summary>Asset. Status: preparing, ready, errored.</summary>
public sealed record MuxAsset(
    string Id,
    string? Status,
    IReadOnlyList<MuxPlaybackId> PlaybackIds,
    decimal? DurationSeconds,
    string? AspectRatio,
    string? UploadId,
    string? Passthrough,
    IReadOnlyList<string> ErrorMessages);

public static class MuxJson
{
    public static string? Str(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object
        && obj.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public static JsonElement? Obj(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object
        && obj.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.Object
            ? v
            : null;

    /// <summary>Upload object (REST "data" or webhook "data" of a video.upload.* event).</summary>
    public static MuxUpload? ParseUpload(JsonElement data)
    {
        var id = Str(data, "id");
        if (string.IsNullOrEmpty(id)) return null;
        var settings = Obj(data, "new_asset_settings");
        var error = Obj(data, "error");
        return new MuxUpload(
            id,
            Str(data, "status"),
            Str(data, "asset_id"),
            settings is { } s ? Str(s, "passthrough") : null,
            error is { } e ? Str(e, "message") : null);
    }

    /// <summary>Asset object (REST "data" or webhook "data" of a video.asset.* event).</summary>
    public static MuxAsset? ParseAsset(JsonElement data)
    {
        var id = Str(data, "id");
        if (string.IsNullOrEmpty(id)) return null;

        var playbackIds = new List<MuxPlaybackId>();
        if (data.TryGetProperty("playback_ids", out var pids) && pids.ValueKind == JsonValueKind.Array)
            foreach (var p in pids.EnumerateArray())
                if (Str(p, "id") is { Length: > 0 } pid)
                    playbackIds.Add(new MuxPlaybackId(pid, Str(p, "policy")));

        decimal? duration = null;
        if (data.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
            && d.TryGetDecimal(out var dv))
            duration = Math.Round(dv, 2, MidpointRounding.AwayFromZero);

        var messages = new List<string>();
        if (Obj(data, "errors") is { } errors)
        {
            if (errors.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array)
                foreach (var m in msgs.EnumerateArray())
                    if (m.ValueKind == JsonValueKind.String && m.GetString() is { Length: > 0 } text)
                        messages.Add(text);
            if (messages.Count == 0 && Str(errors, "type") is { Length: > 0 } type)
                messages.Add(type);
        }

        return new MuxAsset(
            id,
            Str(data, "status"),
            playbackIds,
            duration,
            Str(data, "aspect_ratio"),
            Str(data, "upload_id"),
            Str(data, "passthrough"),
            messages);
    }

    /// <summary>Keeps at most <paramref name="max"/> Unicode code points (never splits a surrogate pair).</summary>
    public static string TruncateCodePoints(string value, int max)
    {
        var sb = new StringBuilder();
        var count = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (count == max) break;
            sb.Append(rune.ToString());
            count++;
        }
        return sb.ToString();
    }

    /// <summary>Truncates to <paramref name="max"/> UTF-16 chars without leaving half a surrogate pair.</summary>
    public static string? Truncate(string? value, int max)
    {
        if (value is null || value.Length <= max) return value;
        var cut = char.IsHighSurrogate(value[max - 1]) ? max - 1 : max;
        return value[..cut];
    }

    public static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
