using Microsoft.Extensions.Logging;

namespace Kurio.Core.Persistence;

internal static partial class JsonStatePersistenceLogMessages
{
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Debug,
        Message = "Saved state for task {TaskId} to {FilePath}")]
    public static partial void LogStateSaved(
        this ILogger logger,
        Guid taskId,
        string filePath);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Error,
        Message = "Failed to save state for task {TaskId}")]
    public static partial void LogStateSaveFailed(
        this ILogger logger,
        Exception exception,
        Guid taskId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Debug,
        Message = "State file not found for task {TaskId}")]
    public static partial void LogStateFileNotFound(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Debug,
        Message = "Loaded state for task {TaskId} from {FilePath}")]
    public static partial void LogStateLoaded(
        this ILogger logger,
        Guid taskId,
        string filePath);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Error,
        Message = "Failed to load state for task {TaskId} from {FilePath}")]
    public static partial void LogStateLoadFailed(
        this ILogger logger,
        Exception exception,
        Guid taskId,
        string filePath);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Warning,
        Message = "Moved corrupted state file to {BackupPath}")]
    public static partial void LogCorruptedStateFileBackedUp(
        this ILogger logger,
        string backupPath);

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Error,
        Message = "Failed to backup corrupted state file")]
    public static partial void LogBackupFailed(
        this ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Debug,
        Message = "State file not found for task {TaskId}, nothing to delete")]
    public static partial void LogStateFileNotFoundForDelete(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 3008,
        Level = LogLevel.Debug,
        Message = "Deleted state for task {TaskId}")]
    public static partial void LogStateDeleted(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 3009,
        Level = LogLevel.Error,
        Message = "Failed to delete state for task {TaskId}")]
    public static partial void LogStateDeleteFailed(
        this ILogger logger,
        Exception exception,
        Guid taskId);

    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Information,
        Message = "Found {Count} state files in {Directory}")]
    public static partial void LogStateFilesFound(
        this ILogger logger,
        int count,
        string directory);

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Debug,
        Message = "Loaded state from {FilePath}")]
    public static partial void LogStateLoadedFromFile(
        this ILogger logger,
        string filePath);

    [LoggerMessage(
        EventId = 3012,
        Level = LogLevel.Error,
        Message = "Failed to load state from {FilePath}, skipping")]
    public static partial void LogStateLoadFailedSkipping(
        this ILogger logger,
        Exception exception,
        string filePath);

    [LoggerMessage(
        EventId = 3013,
        Level = LogLevel.Information,
        Message = "Successfully loaded {Count} download states")]
    public static partial void LogStatesLoaded(
        this ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 3014,
        Level = LogLevel.Error,
        Message = "Failed to load states from directory {Directory}")]
    public static partial void LogStatesLoadFailed(
        this ILogger logger,
        Exception exception,
        string directory);

    [LoggerMessage(
        EventId = 3015,
        Level = LogLevel.Information,
        Message = "Created state directory at {Directory}")]
    public static partial void LogStateDirectoryCreated(
        this ILogger logger,
        string directory);

    [LoggerMessage(
        EventId = 3016,
        Level = LogLevel.Error,
        Message = "Failed to create state directory at {Directory}")]
    public static partial void LogStateDirectoryCreationFailed(
        this ILogger logger,
        Exception exception,
        string directory);
}
