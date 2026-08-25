using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.Storage;

internal static partial class TempFileCleanupServiceLogMessages
{
    [LoggerMessage(
        EventId = 8000,
        Level = LogLevel.Warning,
        Message = "Failed to get info for file {File}")]
    public static partial void LogFileInfoFailed(
        this ILogger logger,
        Exception exception,
        string file);

    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Information,
        Message = "Found {Count} orphaned files totaling {Bytes} bytes")]
    public static partial void LogOrphanedFilesFound(
        this ILogger logger,
        int count,
        long bytes);

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Debug,
        Message = "Deleted orphaned file {File}")]
    public static partial void LogOrphanedFileDeleted(
        this ILogger logger,
        string file);

    [LoggerMessage(
        EventId = 8003,
        Level = LogLevel.Warning,
        Message = "Failed to delete orphaned file {File}")]
    public static partial void LogOrphanedFileDeleteFailed(
        this ILogger logger,
        Exception exception,
        string file);

    [LoggerMessage(
        EventId = 8004,
        Level = LogLevel.Information,
        Message = "Cleanup complete: {Deleted} deleted, {Failed} failed, {Bytes} bytes freed")]
    public static partial void LogCleanupComplete(
        this ILogger logger,
        int deleted,
        int failed,
        long bytes);

    [LoggerMessage(
        EventId = 8005,
        Level = LogLevel.Information,
        Message = "Cleaned up temporary files for task {TaskId}")]
    public static partial void LogTaskFilesCleanedUp(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 8006,
        Level = LogLevel.Warning,
        Message = "Failed to cleanup temporary files for task {TaskId}")]
    public static partial void LogTaskFilesCleanupFailed(
        this ILogger logger,
        Exception exception,
        Guid taskId);

    [LoggerMessage(
        EventId = 8007,
        Level = LogLevel.Debug,
        Message = "Failed to delete empty directory {Directory}")]
    public static partial void LogEmptyDirectoryDeleteFailed(
        this ILogger logger,
        Exception exception,
        string directory);

    [LoggerMessage(
        EventId = 8008,
        Level = LogLevel.Debug,
        Message = "Failed to cleanup empty directories in {Directory}")]
    public static partial void LogEmptyDirectoriesCleanupFailed(
        this ILogger logger,
        Exception exception,
        string directory);
}
