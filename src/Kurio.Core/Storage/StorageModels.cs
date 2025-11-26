namespace Kurio.Core.Storage;

/// <summary>
/// Handle for a temporary download file
/// </summary>
public sealed class TempFileHandle : IDisposable
{
    private bool _disposed;

    public TempFileHandle(Guid taskId, string filePath, FileStream stream, long expectedSize)
    {
        TaskId = taskId;
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        ExpectedSize = expectedSize;
    }

    /// <summary>
    /// Task ID associated with this temporary file
    /// </summary>
    public Guid TaskId { get; }

    /// <summary>
    /// Full path to the temporary file
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// File stream for writing
    /// </summary>
    public FileStream Stream { get; }

    /// <summary>
    /// Expected final size of the file
    /// </summary>
    public long ExpectedSize { get; }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stream?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Information about disk space
/// </summary>
public sealed record DiskSpaceInfo(
    string Path,
    long TotalBytes,
    long AvailableBytes,
    long FreeBytes,
    double UsagePercentage);

/// <summary>
/// Information about orphaned temporary files
/// </summary>
public sealed record OrphanedFilesInfo(
    int FileCount,
    long TotalBytes,
    List<OrphanedFile> Files);

/// <summary>
/// Information about a single orphaned file
/// </summary>
public sealed record OrphanedFile(
    string Path,
    long Size,
    DateTime LastModified,
    Guid? TaskId);

/// <summary>
/// Result of cleanup operation
/// </summary>
public sealed record CleanupResult(
    int FilesDeleted,
    long BytesFreed,
    int FailedDeletions);
