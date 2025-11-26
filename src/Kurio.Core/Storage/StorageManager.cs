using Kurio.Core.Abstractions;
using Kurio.Core.Models;

namespace Kurio.Core.Storage;

/// <summary>
///     Manages file system operations for downloads.
/// </summary>
public sealed class StorageManager : IStorageManager
{
    private readonly string _stateDirectory;
    private readonly string _tempDirectory;
    private readonly IPlatformPathProvider _pathProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StorageManager" /> class.
    /// </summary>
    /// <param name="tempDirectory">The directory for temporary download files.</param>
    /// <param name="stateDirectory">The directory for state files.</param>
    /// <param name="pathProvider">Platform path provider (optional).</param>
    public StorageManager(
        string tempDirectory, 
        string stateDirectory,
        IPlatformPathProvider? pathProvider = null)
    {
        _tempDirectory = tempDirectory ?? throw new ArgumentNullException(nameof(tempDirectory));
        _stateDirectory = stateDirectory ?? throw new ArgumentNullException(nameof(stateDirectory));
        _pathProvider = pathProvider ?? new PlatformPathProvider();

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
        string taskDirectory = Path.Combine(_tempDirectory, taskId.ToString());
        Directory.CreateDirectory(taskDirectory);

        string tempFilePath = Path.Combine(taskDirectory, "download.part");

        // Pre-allocate file to the expected size for better performance
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
        // Use FileStream with FileShare.Write to allow multiple segments to write concurrently
        await using FileStream fileStream = new(
            filePath,
            FileMode.Open,
            FileAccess.Write,
            FileShare.Write,
            4096,
            true);

        fileStream.Seek(offset, SeekOrigin.Begin);
        await fileStream.WriteAsync(data.AsMemory(0, count), cancellationToken);
        await fileStream.FlushAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> CommitDownloadAsync(
        string tempFilePath,
        string destinationDirectory,
        string fileName,
        FileNamingPolicy namingPolicy,
        CancellationToken cancellationToken = default)
    {
        // Ensure destination directory exists
        Directory.CreateDirectory(destinationDirectory);

        string destinationPath = Path.Combine(destinationDirectory, fileName);

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

        // Move file atomically (rename operation)
        await Task.Run(() =>
        {
            // On some file systems, File.Move might not be truly atomic
            // For production, consider using platform-specific APIs
            File.Move(tempFilePath, destinationPath, namingPolicy == FileNamingPolicy.Overwrite);
        }, cancellationToken);

        return destinationPath;
    }

    /// <inheritdoc />
    public Task<long> GetAvailableDiskSpaceAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        DriveInfo driveInfo = new(Path.GetPathRoot(path) ?? path);
        return Task.FromResult(driveInfo.AvailableFreeSpace);
    }

    /// <inheritdoc />
    public Task CleanupTemporaryFilesAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        string taskDirectory = Path.Combine(_tempDirectory, taskId.ToString());

        if (Directory.Exists(taskDirectory))
        {
            Directory.Delete(taskDirectory, true);
        }

        // Also clean up state file if it exists
        string stateFilePath = Path.Combine(_stateDirectory, $"{taskId}.json");
        if (File.Exists(stateFilePath))
        {
            File.Delete(stateFilePath);
        }

        return Task.CompletedTask;
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

        string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);

        int counter = 1;
        string newFilePath;

        do
        {
            string newFileName = $"{fileNameWithoutExtension}({counter}){extension}";
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
        var availableSpace = await GetAvailableDiskSpaceAsync(path, cancellationToken);
        return availableSpace >= (requiredBytes + minimumFreeSpaceBuffer);
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
            throw new ArgumentException("Filename cannot be empty", nameof(fileName));

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
            sanitized = "download";

        return sanitized;
    }

    /// <summary>
    ///     Gets the appropriate directory for a category
    /// </summary>
    public string GetCategoryDirectory(string baseDirectory, string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return baseDirectory;

        var sanitizedCategory = SanitizeFileName(category);
        return Path.Combine(baseDirectory, sanitizedCategory);
    }
}
