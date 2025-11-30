using Microsoft.Extensions.Logging;

namespace Kurio.Core.Storage;

/// <summary>
///     Manages temporary file cleanup operations
/// </summary>
public interface ITempFileCleanupService
{
    /// <summary>
    ///     Scans for orphaned temporary files
    /// </summary>
    Task<OrphanedFilesInfo> ScanForOrphanedFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cleans up orphaned files older than the specified threshold
    /// </summary>
    Task<CleanupResult> CleanupOrphanedFilesAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cleans up temporary files for a specific task
    /// </summary>
    Task CleanupTaskFilesAsync(Guid taskId, CancellationToken cancellationToken = default);
}

/// <summary>
///     Default implementation of temp file cleanup service
/// </summary>
public sealed class TempFileCleanupService : ITempFileCleanupService
{
    private readonly ILogger<TempFileCleanupService> _logger;
    private readonly string _tempDirectory;

    public TempFileCleanupService(
        string tempDirectory,
        ILogger<TempFileCleanupService> logger)
    {
        _tempDirectory = tempDirectory ?? throw new ArgumentNullException(nameof(tempDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<OrphanedFilesInfo> ScanForOrphanedFilesAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_tempDirectory))
        {
            return Task.FromResult(new OrphanedFilesInfo(0, 0, new List<OrphanedFile>()));
        }

        List<OrphanedFile> orphanedFiles = new();
        long totalBytes = 0;

        var taskDirectories = Directory.GetDirectories(_tempDirectory);

        foreach (var taskDir in taskDirectories)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var dirName = Path.GetFileName(taskDir);
            Guid? taskId = Guid.TryParse(dirName, out var id) ? id : null;

            var files = Directory.GetFiles(taskDir, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    FileInfo fileInfo = new(file);
                    orphanedFiles.Add(new OrphanedFile(
                        file,
                        fileInfo.Length,
                        fileInfo.LastWriteTime,
                        taskId));
                    totalBytes += fileInfo.Length;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get info for file {File}", file);
                }
            }
        }

        _logger.LogInformation("Found {Count} orphaned files totaling {Bytes} bytes",
            orphanedFiles.Count, totalBytes);

        return Task.FromResult(new OrphanedFilesInfo(orphanedFiles.Count, totalBytes, orphanedFiles));
    }

    public async Task<CleanupResult> CleanupOrphanedFilesAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default)
    {
        var orphanedInfo = await ScanForOrphanedFilesAsync(cancellationToken);

        var threshold = DateTime.Now - olderThan;
        var filesToDelete = orphanedInfo.Files
            .Where(f => f.LastModified < threshold)
            .ToList();

        var deleted = 0;
        var failed = 0;
        long bytesFreed = 0;

        foreach (var file in filesToDelete)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                File.Delete(file.Path);
                deleted++;
                bytesFreed += file.Size;
                _logger.LogDebug("Deleted orphaned file {File}", file.Path);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(ex, "Failed to delete orphaned file {File}", file.Path);
            }
        }

        // Clean up empty directories
        CleanupEmptyDirectories(_tempDirectory);

        _logger.LogInformation(
            "Cleanup complete: {Deleted} deleted, {Failed} failed, {Bytes} bytes freed",
            deleted, failed, bytesFreed);

        return new CleanupResult(deleted, bytesFreed, failed);
    }

    public Task CleanupTaskFilesAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var taskDirectory = Path.Combine(_tempDirectory, taskId.ToString());

        if (!Directory.Exists(taskDirectory))
        {
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Directory.Delete(taskDirectory, true);
            _logger.LogInformation("Cleaned up temporary files for task {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup temporary files for task {TaskId}", taskId);
        }

        return Task.CompletedTask;
    }

    private void CleanupEmptyDirectories(string directory)
    {
        try
        {
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                CleanupEmptyDirectories(subDir);

                if (!Directory.EnumerateFileSystemEntries(subDir).Any())
                {
                    try
                    {
                        Directory.Delete(subDir);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to delete empty directory {Directory}", subDir);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to cleanup empty directories in {Directory}", directory);
        }
    }
}
