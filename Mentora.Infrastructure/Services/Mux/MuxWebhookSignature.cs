using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Mentora.Infrastructure.Services.Mux;

/// <summary>
/// Lot 12.3a — MUX webhook signature: header "Mux-Signature: t=&lt;unix seconds&gt;,v1=&lt;hex&gt;[,v1=...]",
/// v1 = lowercase hex HMAC SHA256 (key = webhook secret as UTF-8) of ASCII "&lt;t&gt;." followed by the raw body
/// bytes exactly as received. Comparison in constant time on the decoded bytes.
/// </summary>
public static class MuxWebhookSignature
{
    public const string ReasonMissing = "missing";
    public const string ReasonFormat = "format";
    public const string ReasonStale = "stale";
    public const string ReasonSignature = "signature";

    /// <summary>Null when the signature is valid, else one of the Reason* values.</summary>
    public static string? Verify(string? header, byte[] body, string secret, int toleranceSeconds, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(header))
            return ReasonMissing;

        string? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) return ReasonFormat;
            var key = part[..eq];
            var value = part[(eq + 1)..];
            if (key == "t")
            {
                if (timestamp is not null) return ReasonFormat;
                timestamp = value;
            }
            else if (key == "v1")
            {
                signatures.Add(value);
            }
        }

        if (timestamp is null || signatures.Count == 0
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var t))
            return ReasonFormat;

        if (Math.Abs(now.ToUnixTimeSeconds() - t) > toleranceSeconds)
            return ReasonStale;

        var prefix = Encoding.ASCII.GetBytes(timestamp + ".");
        var message = new byte[prefix.Length + body.Length];
        Buffer.BlockCopy(prefix, 0, message, 0, prefix.Length);
        Buffer.BlockCopy(body, 0, message, prefix.Length, body.Length);
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), message);

        var match = false;
        foreach (var candidate in signatures)
        {
            byte[] decoded;
            try
            {
                decoded = Convert.FromHexString(candidate);
            }
            catch (FormatException)
            {
                continue;
            }
            // Every candidate is compared (no early exit), each in constant time.
            if (decoded.Length == expected.Length && CryptographicOperations.FixedTimeEquals(decoded, expected))
                match = true;
        }
        return match ? null : ReasonSignature;
    }
}
