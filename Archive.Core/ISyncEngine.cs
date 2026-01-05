namespace Archive.Core;

/// <summary>
/// Defines the contract for directory synchronization operations.
/// </summary>
public interface ISyncEngine
{
    /// <summary>
    /// Synchronizes the source directory to the destination directory.
    /// </summary>
    /// <param name="sourcePath">The source directory path.</param>
    /// <param name="destinationPath">The destination directory path.</param>
    /// <param name="options">Synchronization options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the synchronization result.</returns>
    Task<SyncResult> SynchronizeAsync(
        string sourcePath,
        string destinationPath,
        SyncOptions options,
        CancellationToken cancellationToken = default);
}
