using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Services.Audio;

/// <summary>
/// Lot 13.3. Voice message files on disk under Audio:StoragePath (13.A): final files at
/// "{conversationId}/{messageId}.aac", uploads first written to ".incoming/{guid}.upload" on the same file
/// system, then moved without overwrite. Every full path is checked to stay strictly inside the root.
/// </summary>
public sealed class DiskVoiceMessageStorage(
    IOptions<AudioOptions> options,
    IAudioAvailability availability,
    ILogger<DiskVoiceMessageStorage> logger) : IVoiceMessageStorage
{
    private const string IncomingDirectoryName = ".incoming";

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <inheritdoc />
    public string RelativePathFor(Guid conversationId, Guid messageId)
        => $"{conversationId.ToString("D")}/{messageId.ToString("D")}.aac";

    /// <inheritdoc />
    public string CreateIncomingFilePath()
    {
        var root = Root();
        Directory.CreateDirectory(Resolve(root, IncomingDirectoryName));
        return Resolve(root, $"{IncomingDirectoryName}/{Guid.NewGuid().ToString("D")}.upload");
    }

    /// <inheritdoc />
    public Task CommitAsync(string incomingFilePath, string relativePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var root = Root();
        var source = EnsureInside(root, Path.GetFullPath(incomingFilePath));
        var target = Resolve(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(source, target, overwrite: false);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Stream OpenRead(string relativePath)
        => new FileStream(Resolve(Root(), relativePath), FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

    /// <inheritdoc />
    public bool Exists(string relativePath) => File.Exists(Resolve(Root(), relativePath));

    /// <inheritdoc />
    public void Delete(string relativePath)
    {
        var fullPath = Resolve(Root(), relativePath);
        try
        {
            File.Delete(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("[AUDIO] could not delete stored file {RelativePath}: {Error}", relativePath, ex.GetType().Name);
        }
    }

    /// <inheritdoc />
    public void DeleteIncoming(string incomingFilePath)
    {
        var fullPath = EnsureInside(Root(), Path.GetFullPath(incomingFilePath));
        try
        {
            File.Delete(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("[AUDIO] could not delete incoming file {FileName}: {Error}",
                Path.GetFileName(fullPath), ex.GetType().Name);
        }
    }

    /// <summary>Full path of the storage root, without a trailing separator.</summary>
    private string Root()
    {
        if (!availability.StorageOk)
            throw new AudioNotConfiguredException();
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Value.StoragePath));
    }

    private static string Resolve(string root, string relativePath)
        => EnsureInside(root, Path.GetFullPath(Path.Combine(root, relativePath)));

    /// <summary>The full path must start with the root plus a directory separator (no traversal).</summary>
    private static string EnsureInside(string root, string fullPath)
    {
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
            throw new InvalidOperationException("Audio storage path is outside the storage root.");
        return fullPath;
    }
}
