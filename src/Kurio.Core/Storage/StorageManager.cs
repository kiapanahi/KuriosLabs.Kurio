namespace Kurio.Core.Storage;

using System.IO;

using Kurio.Core.Abstractions;
using Kurio.Core.Models;

/// <summary>
/// Manages file system operations for downloads.
/// </summary>
public sealed class StorageManager : IStorageManager
{
    private readonly string _tempDirectory;
    private readonly string _stateDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageManager"/> class.
    /// </summary>
    /// <param name="tempDirectory">The directory for temporary download files.</param>
    /// <param name="stateDirectory">The directory for state files.</param>
    public StorageManager(string tempDirectory, string stateDirectory)
    {
        _tempDirectory = tempDirectory ?? throw new ArgumentNullException(nameof(tempDirectory));
        _stateDirectory = stateDirectory ?? throw new ArgumentNullException(nameof(stateDirectory));

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

        // Pre-allocate file to the expected size for better performance
        using (var fileStream = new FileStream(
            tempFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true))
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
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Write,
            FileShare.Write,
            bufferSize: 4096,
            useAsync: true);

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

        var destinationPath = Path.Combine(destinationDirectory, fileName);

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
            File.Move(tempFilePath, destinationPath, overwrite: namingPolicy == FileNamingPolicy.Overwrite);
        }, cancellationToken);

        return destinationPath;
    }

    /// <inheritdoc />
    public Task<long> GetAvailableDiskSpaceAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var driveInfo = new DriveInfo(Path.GetPathRoot(path) ?? path);
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
            Directory.Delete(taskDirectory, recursive: true);
        }

        // Also clean up state file if it exists
        var stateFilePath = Path.Combine(_stateDirectory, $"{taskId}.json");
        if (File.Exists(stateFilePath))
        {
            File.Delete(stateFilePath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a unique file path by adding a numeric suffix if the file already exists.
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
        }
        while (File.Exists(newFilePath));

        return newFilePath;
    }
}
