using System.Collections.Concurrent;

using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Core.Storage;

/// <summary>
///     Manages file system operations for downloads.
/// </summary>
public sealed class StorageManager : IStorageManager
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
    private readonly StorageOptions _options;
    private readonly IPlatformPathProvider _pathProvider;
    private readonly string _stateDirectory;
    private readonly string _tempDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StorageManager" /> class.
    /// </summary>
    /// <param name="tempDirectory">The directory for temporary download files.</param>
    /// <param name="stateDirectory">The directory for state files.</param>
    /// <param name="pathProvider">Platform path provider (optional).</param>
    /// <param name="options">Storage options (optional).</param>
    public StorageManager(
        string tempDirectory,
        string stateDirectory,
        IPlatformPathProvider? pathProvider = null,
        StorageOptions? options = null)
    {
        _tempDirectory = tempDirectory ?? throw new ArgumentNullException(nameof(tempDirectory));
        _stateDirectory = stateDirectory ?? throw new ArgumentNullException(nameof(stateDirectory));
        _pathProvider = pathProvider ?? new PlatformPathProvider();
        _options = options ?? StorageOptions.Default;

        // Expand paths
        _tempDirectory = _pathProvider.ExpandPath(_tempDirectory);
        _stateDirectory = _pathProvider.ExpandPath(_stateDirectory);

        // Ensure directories exist
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_stateDirectory);
    }

    /// <inheritdoc />
    public Task<string> CreateTemporaryFileAsync(
        Guid taskId,
        string fileName,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        var taskDirectory = Path.Combine(_tempDirectory, taskId.ToString());
        Directory.CreateDirectory(taskDirectory);

        var tempFilePath = Path.Combine(taskDirectory, "download.part");

        // In PerSegmentFiles mode, we don't create/pre-allocate the main file
        // Segments will be written to individual files and merged later
        if (_options.Mode == StorageMode.PerSegmentFiles)
        {
            // Just return the path where the merged file will be created
            // Segment files will be created by SegmentManager as needed
            return Task.FromResult(tempFilePath);
        }

        // In SingleFile mode, pre-allocate the file to the expected size for better performance
        using (FileStream fileStream = new(
                   tempFilePath,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None,
                   4096,
                   true))
        {
            if (fileSize > 0)
            {
                // Set the file length to pre-allocate disk space
                fileStream.SetLength(fileSize);
            }
        }

        return Task.FromResult(tempFilePath);
    }

    /// <inheritdoc />
    public async Task WriteSegmentAsync(
        string filePath,
        long offset,
        byte[] data,
        int count,
        CancellationToken cancellationToken = default)
    {
        // Get or create lock for this file
        var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));

        // Acquire exclusive lock for write operation
        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            FileStream fileStream = new(
                filePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None, // Exclusive access during write
                _options.WriteBufferSize,
                FileOptions.WriteThrough | FileOptions.Asynchronous);

            await using (fileStream.ConfigureAwait(false))
            {
                fileStream.Seek(offset, SeekOrigin.Begin);
                await fileStream.WriteAsync(data.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                // Optional: Verify write succeeded
                if (_options.VerifyWrites)
                {
                    fileStream.Seek(offset, SeekOrigin.Begin);
                    var verifyBuffer = new byte[Math.Min(4096, count)];
                    var bytesRead = await fileStream.ReadAsync(verifyBuffer.AsMemory(), cancellationToken).ConfigureAwait(false);

                    if (!data.AsSpan(0, bytesRead).SequenceEqual(verifyBuffer.AsSpan(0, bytesRead)))
                    {
                        throw new IOException($"Write verification failed at offset {offset}");
                    }
                }
            }
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<string> CommitDownloadAsync(
        string tempFilePath,
        string destinationDirectory,
        string fileName,
        FileNamingPolicy namingPolicy,
        CancellationToken cancellationToken = default)
    {
        // Expand destination directory to handle ~ and environment variables
        var expandedDestination = _pathProvider.ExpandPath(destinationDirectory);

        // Ensure destination directory exists
        Directory.CreateDirectory(expandedDestination);

        var destinationPath = Path.Combine(expandedDestination, fileName);

        // Handle file naming conflicts
        destinationPath = namingPolicy switch
        {
            FileNamingPolicy.Overwrite => destinationPath,
            FileNamingPolicy.AutoRename => GetUniqueFilePath(destinationPath),
            FileNamingPolicy.Skip => File.Exists(destinationPath)
                ? throw new InvalidOperationException($"File already exists: {destinationPath}")
                : destinationPath,
            FileNamingPolicy.Prompt => throw new NotSupportedException(
                "Prompt policy requires UI integration and is not supported in core library"),
            _ => throw new ArgumentException($"Unknown naming policy: {namingPolicy}", nameof(namingPolicy))
        };

        cancellationToken.ThrowIfCancellationRequested();

        // Move file atomically (rename operation)
        // On some file systems, File.Move might not be truly atomic
        // For production, consider using platform-specific APIs
        File.Move(tempFilePath, destinationPath, namingPolicy == FileNamingPolicy.Overwrite);

        return Task.FromResult(destinationPath);
    }

    /// <inheritdoc />
    public Task<long> GetAvailableDiskSpaceAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        // Expand path to handle ~ and environment variables
        var expandedPath = _pathProvider.ExpandPath(path);
        DriveInfo driveInfo = new(Path.GetPathRoot(expandedPath) ?? expandedPath);
        return Task.FromResult(driveInfo.AvailableFreeSpace);
    }

    /// <inheritdoc />
    public Task CleanupTemporaryFilesAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var taskDirectory = Path.Combine(_tempDirectory, taskId.ToString());

        if (Directory.Exists(taskDirectory))
        {
            Directory.Delete(taskDirectory, true);
        }

        // Also clean up state file if it exists
        var stateFilePath = Path.Combine(_stateDirectory, $"{taskId}.json");
        if (File.Exists(stateFilePath))
        {
            File.Delete(stateFilePath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<string> CreateSegmentFileAsync(
        Guid taskId,
        int segmentIndex,
        long segmentSize,
        CancellationToken cancellationToken = default)
    {
        var taskDirectory = Path.Combine(_tempDirectory, taskId.ToString());
        Directory.CreateDirectory(taskDirectory);

        var segmentFilePath = Path.Combine(taskDirectory, $"segment_{segmentIndex:D4}.part");

        FileStream fileStream = new(
            segmentFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous);

        await using (fileStream.ConfigureAwait(false))
        {
            // Pre-allocate space for the segment
            if (segmentSize > 0)
            {
                fileStream.SetLength(segmentSize);
            }

            return segmentFilePath;
        }
    }

    /// <inheritdoc />
    public async Task MergeSegmentFilesAsync(
        Guid taskId,
        string finalPath,
        int segmentCount,
        CancellationToken cancellationToken = default)
    {
        var taskDirectory = Path.Combine(_tempDirectory, taskId.ToString());

        // Ensure the parent directory of finalPath exists
        var finalDirectory = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(finalDirectory))
        {
            Directory.CreateDirectory(finalDirectory);
        }

        if (segmentCount == 1)
        {
            var singleSegmentPath = Path.Combine(taskDirectory, "segment_0000.part");
            if (!File.Exists(singleSegmentPath))
            {
                throw new FileNotFoundException($"Segment file not found: {singleSegmentPath}");
            }

            // A single segment already holds the complete payload; rename it into
            // place instead of copying (the payload can be arbitrarily large when
            // the server does not support ranges).
            File.Move(singleSegmentPath, finalPath, true);
            return;
        }

        FileStream outputStream = new(
            finalPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            1048576, // 1MB buffer for fast merge
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using (outputStream.ConfigureAwait(false))
        {
            for (var i = 0; i < segmentCount; i++)
            {
                var segmentPath = Path.Combine(taskDirectory, $"segment_{i:D4}.part");

                if (!File.Exists(segmentPath))
                {
                    throw new FileNotFoundException($"Segment file not found: {segmentPath}");
                }

                FileStream inputStream = new(
                    segmentPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    1048576,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                await using (inputStream.ConfigureAwait(false))
                {
                    await inputStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                }
            }

            await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Cleanup segment files if configured
            if (_options.CleanupSegmentFiles)
            {
                for (var i = 0; i < segmentCount; i++)
                {
                    var segmentPath = Path.Combine(taskDirectory, $"segment_{i:D4}.part");
                    try
                    {
                        if (File.Exists(segmentPath))
                        {
                            File.Delete(segmentPath);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyWriteAsync(
        string filePath,
        long offset,
        byte[] expectedData,
        int count,
        CancellationToken cancellationToken = default)
    {
        FileStream fileStream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using (fileStream.ConfigureAwait(false))
        {
            fileStream.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[count];
            var totalRead = 0;

            while (totalRead < count)
            {
                var read = await fileStream.ReadAsync(
                    buffer.AsMemory(totalRead, count - totalRead),
                    cancellationToken).ConfigureAwait(false);

                if (read == 0)
                {
                    // Unexpected end of file
                    return false;
                }

                totalRead += read;
            }

            return expectedData.AsSpan(0, count).SequenceEqual(buffer.AsSpan(0, count));
        }
    }

    /// <summary>
    ///     Gets a unique file path by adding a numeric suffix if the file already exists.
    /// </summary>
    private static string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        var counter = 1;
        string newFilePath;

        do
        {
            var newFileName = $"{fileNameWithoutExtension}({counter}){extension}";
            newFilePath = Path.Combine(directory, newFileName);
            counter++;
        } while (File.Exists(newFilePath));

        return newFilePath;
    }

    /// <summary>
    ///     Checks if there's sufficient disk space for a download
    /// </summary>
    public async Task<bool> HasSufficientSpaceAsync(
        string path,
        long requiredBytes,
        long minimumFreeSpaceBuffer,
        CancellationToken cancellationToken = default)
    {
        var availableSpace = await GetAvailableDiskSpaceAsync(path, cancellationToken).ConfigureAwait(false);
        return availableSpace >= requiredBytes + minimumFreeSpaceBuffer;
    }

    /// <summary>
    ///     Resolves filename conflicts based on naming policy
    /// </summary>
    public string ResolveFileNameConflict(
        string directory,
        string fileName,
        FileNamingPolicy policy)
    {
        var fullPath = Path.Combine(directory, fileName);

        return policy switch
        {
            FileNamingPolicy.Overwrite => fullPath,
            FileNamingPolicy.AutoRename => GetUniqueFilePath(fullPath),
            FileNamingPolicy.Skip => fullPath,
            _ => throw new ArgumentException($"Unsupported naming policy: {policy}", nameof(policy))
        };
    }

    /// <summary>
    ///     Sanitizes a filename for the current platform
    /// </summary>
    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Filename cannot be empty", nameof(fileName));
        }

        var invalidChars = _pathProvider.GetInvalidFileNameChars();
        var sanitized = fileName;

        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        // Remove leading/trailing spaces and dots (problematic on Windows)
        sanitized = sanitized.Trim(' ', '.');

        // Ensure not empty after sanitization
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "download";
        }

        return sanitized;
    }

    /// <summary>
    ///     Gets the appropriate directory for a category
    /// </summary>
    public string GetCategoryDirectory(string baseDirectory, string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return baseDirectory;
        }

        var sanitizedCategory = SanitizeFileName(category);
        return Path.Combine(baseDirectory, sanitizedCategory);
    }
}
