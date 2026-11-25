using System.Security.Cryptography;
using System.Text.Json;

namespace Mentora.Infrastructure.Services.Push;

/// <summary>
/// Lot 14.9b. The parsed Google service account key file (a secret). Only the API process reads it. Nothing
/// of it (path, client_email, private key) is ever logged or put in a message: <see cref="TryLoad"/> returns a
/// fixed failure text.
/// </summary>
public sealed class FirebaseServiceAccount : IDisposable
{
    private FirebaseServiceAccount(string clientEmail, RSA key)
    {
        ClientEmail = clientEmail;
        Key = key;
    }

    public string ClientEmail { get; }
    public RSA Key { get; }

    public void Dispose() => Key.Dispose();

    /// <summary>Returns null and the account when valid, otherwise a failure message that never contains the path or the content.</summary>
    public static string? TryLoad(string path, string expectedProjectId, out FirebaseServiceAccount? account)
    {
        account = null;

        if (!File.Exists(path))
            return "Firebase:ServiceAccountPath does not point to an existing file.";

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return "Firebase:ServiceAccountPath file cannot be read as JSON.";
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return "Firebase service account file must be a JSON object.";

            if (Str(root, "type") != "service_account")
                return "Firebase service account file must have type service_account.";
            if (!string.Equals(Str(root, "project_id"), expectedProjectId, StringComparison.Ordinal))
                return "Firebase service account project_id must equal Firebase:ProjectId.";

            var clientEmail = Str(root, "client_email");
            if (string.IsNullOrWhiteSpace(clientEmail))
                return "Firebase service account file has no client_email.";
            var privateKey = Str(root, "private_key");
            if (string.IsNullOrWhiteSpace(privateKey))
                return "Firebase service account file has no private_key.";

            var rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(privateKey);
            }
            catch (Exception ex) when (ex is ArgumentException or CryptographicException)
            {
                rsa.Dispose();
                return "Firebase service account private_key cannot be imported.";
            }

            account = new FirebaseServiceAccount(clientEmail!, rsa);
            return null;
        }
    }

    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
