namespace Mentora.Core.Interfaces;

/// <summary>
/// Lot 13.3. Disk storage of voice message files under Audio:StoragePath. Every member except
/// <see cref="RelativePathFor"/> throws <see cref="Mentora.Core.Exceptions.AudioNotConfiguredException"/>
/// when the storage is not available, and <see cref="InvalidOperationException"/> when a path would
/// resolve outside the storage root.
/// </summary>
public interface IVoiceMessageStorage
{
    /// <summary>Relative path of a stored voice message: "{conversationId}/{messageId}.aac" (lowercase GUIDs).</summary>
    string RelativePathFor(Guid conversationId, Guid messageId);

    /// <summary>
    /// Full path of a new incoming file "&lt;root&gt;/.incoming/&lt;guid&gt;.upload" (directory created on
    /// demand, same file system as the final files so the commit move is atomic).
    /// </summary>
    string CreateIncomingFilePath();

    /// <summary>Moves an incoming file to its final relative path (never overwrites).</summary>
    Task CommitAsync(string incomingFilePath, string relativePath, CancellationToken ct);

    /// <summary>Opens a stored file for asynchronous reading (shared read).</summary>
    Stream OpenRead(string relativePath);

    /// <summary>True when the stored file exists.</summary>
    bool Exists(string relativePath);

    /// <summary>Deletes a stored file, best effort (a failure is logged as a warning).</summary>
    void Delete(string relativePath);

    /// <summary>Deletes an incoming file, best effort (a failure is logged as a warning).</summary>
    void DeleteIncoming(string incomingFilePath);
}
