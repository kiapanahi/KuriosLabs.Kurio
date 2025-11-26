using Kurio.Core.Models;

namespace Kurio.Core.Abstractions;

/// <summary>
///     Manages file system operations for downloads.
/// </summary>
public interface IStorageManager
{
    /// <summary>
    ///     Creates a temporary file for storing partial download data.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="fileName">The target file name.</param>
    /// <param name="fileSize">The expected file size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full path to the created temporary file.</returns>
    Task<string> CreateTemporaryFileAsync(
        Guid taskId,
        string fileName,
        long fileSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Writes data to a specific offset in the file.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="offset">The byte offset where to write.</param>
    /// <param name="data">The data buffer to write.</param>
    /// <param name="count">The number of bytes to write from the buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteSegmentAsync(
        string filePath,
        long offset,
        byte[] data,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Moves the temporary file to the final destination atomically.
    /// </summary>
    /// <param name="tempFilePath">The path to the temporary file.</param>
    /// <param name="destinationDirectory">The destination directory.</param>
    /// <param name="fileName">The target file name.</param>
    /// <param name="namingPolicy">The file naming policy for conflicts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final path of the committed file.</returns>
    Task<string> CommitDownloadAsync(
        string tempFilePath,
        string destinationDirectory,
        string fileName,
        FileNamingPolicy namingPolicy,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the available disk space at the specified path.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Available space in bytes.</returns>
    Task<long> GetAvailableDiskSpaceAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cleans up all temporary files associated with a task.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CleanupTemporaryFilesAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a separate file for a segment (per-segment mode).
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="segmentIndex">The segment index.</param>
    /// <param name="segmentSize">The size of the segment in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full path to the created segment file.</returns>
    Task<string> CreateSegmentFileAsync(
        Guid taskId,
        int segmentIndex,
        long segmentSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Merges all segment files into a single file (per-segment mode).
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="finalPath">The path to the final merged file.</param>
    /// <param name="segmentCount">The number of segments to merge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MergeSegmentFilesAsync(
        Guid taskId,
        string finalPath,
        int segmentCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies that data was written correctly to a file.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="offset">The byte offset to verify.</param>
    /// <param name="expectedData">The expected data.</param>
    /// <param name="count">The number of bytes to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if verification succeeded, false otherwise.</returns>
    Task<bool> VerifyWriteAsync(
        string filePath,
        long offset,
        byte[] expectedData,
        int count,
        CancellationToken cancellationToken = default);
}
