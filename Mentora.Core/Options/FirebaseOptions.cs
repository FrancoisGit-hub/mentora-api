namespace Mentora.Core.Options;

/// <summary>
/// Lot 14.9b. Firebase Cloud Messaging settings, section "Firebase". ProjectId is not a secret (appsettings.json).
/// ServiceAccountPath comes from the environment variable Firebase__ServiceAccountPath only and points to the
/// service account key file, which is a secret kept outside the repository: its path and content are never
/// logged or put in a message. Empty path: push is not configured (allowed in Development only).
/// </summary>
public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string ServiceAccountPath { get; init; } = "";

    public string ProjectId { get; init; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServiceAccountPath);
}
