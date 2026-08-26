using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.Statistics;

internal static partial class JsonDownloadHistoryRepositoryLogMessages
{
    [LoggerMessage(
        EventId = 6100,
        Level = LogLevel.Debug,
        Message = "Added history entry for download {Id}: {FileName}")]
    public static partial void LogHistoryEntryAdded(
        this ILogger logger,
        Guid id,
        string fileName);

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Information,
        Message = "Cleaned up {Count} old history entries older than {Cutoff}")]
    public static partial void LogOldEntriesCleanedUp(
        this ILogger logger,
        int count,
        DateTime cutoff);

    [LoggerMessage(
        EventId = 6102,
        Level = LogLevel.Debug,
        Message = "Deleted history entry {Id}")]
    public static partial void LogHistoryEntryDeleted(
        this ILogger logger,
        Guid id);

    [LoggerMessage(
        EventId = 6103,
        Level = LogLevel.Information,
        Message = "Cleared all download history")]
    public static partial void LogAllHistoryCleared(
        this ILogger logger);

    [LoggerMessage(
        EventId = 6104,
        Level = LogLevel.Debug,
        Message = "Loaded {Count} history entries from {Path}")]
    public static partial void LogHistoryEntriesLoaded(
        this ILogger logger,
        int count,
        string path);

    [LoggerMessage(
        EventId = 6105,
        Level = LogLevel.Error,
        Message = "Failed to load history from {Path}, starting fresh")]
    public static partial void LogHistoryLoadFailed(
        this ILogger logger,
        Exception exception,
        string path);

    [LoggerMessage(
        EventId = 6106,
        Level = LogLevel.Warning,
        Message = "Moved corrupted history file to {BackupPath}")]
    public static partial void LogCorruptedHistoryFileBackedUp(
        this ILogger logger,
        string backupPath);

    [LoggerMessage(
        EventId = 6107,
        Level = LogLevel.Error,
        Message = "Failed to backup corrupted history file")]
    public static partial void LogHistoryBackupFailed(
        this ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 6108,
        Level = LogLevel.Error,
        Message = "Failed to save history to {Path}")]
    public static partial void LogHistorySaveFailed(
        this ILogger logger,
        Exception exception,
        string path);

    [LoggerMessage(
        EventId = 6109,
        Level = LogLevel.Information,
        Message = "Created history directory at {Directory}")]
    public static partial void LogHistoryDirectoryCreated(
        this ILogger logger,
        string directory);

    [LoggerMessage(
        EventId = 6110,
        Level = LogLevel.Error,
        Message = "Failed to create history directory at {Directory}")]
    public static partial void LogHistoryDirectoryCreationFailed(
        this ILogger logger,
        Exception exception,
        string directory);
}
