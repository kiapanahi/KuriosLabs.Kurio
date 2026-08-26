using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.Engine;

internal static partial class DownloadEngineLogMessages
{
    // Scheduler errors (4000-4019)
    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Error,
        Message = "Error in download scheduler")]
    public static partial void LogSchedulerError(
        this ILogger logger,
        Exception exception);

    // Download execution (4020-4049)
    [LoggerMessage(
        EventId = 4020,
        Level = LogLevel.Information,
        Message = "Starting download for task {TaskId}: {Url}")]
    public static partial void LogDownloadStarting(
        this ILogger logger,
        Guid taskId,
        string url);

    [LoggerMessage(
        EventId = 4021,
        Level = LogLevel.Debug,
        Message = "Analyzing download for task {TaskId}")]
    public static partial void LogDownloadAnalyzing(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 4022,
        Level = LogLevel.Information,
        Message = "Task {TaskId}: File size: {FileSize} bytes, Suggested name: {FileName}")]
    public static partial void LogDownloadMetadata(
        this ILogger logger,
        Guid taskId,
        long fileSize,
        string? fileName);

    [LoggerMessage(
        EventId = 4023,
        Level = LogLevel.Error,
        Message = "Task {TaskId}: Insufficient disk space. Required: {Required} bytes, Available: {Available} bytes")]
    public static partial void LogInsufficientDiskSpace(
        this ILogger logger,
        Guid taskId,
        long required,
        long available);

    [LoggerMessage(
        EventId = 4024,
        Level = LogLevel.Debug,
        Message = "Task {TaskId}: Created temporary file at {TempFilePath}")]
    public static partial void LogTempFileCreated(
        this ILogger logger,
        Guid taskId,
        string tempFilePath);

    [LoggerMessage(
        EventId = 4025,
        Level = LogLevel.Information,
        Message = "Task {TaskId}: Starting download with {SegmentCount} segments")]
    public static partial void LogDownloadBeginning(
        this ILogger logger,
        Guid taskId,
        int segmentCount);

    [LoggerMessage(
        EventId = 4026,
        Level = LogLevel.Information,
        Message = "Merging {SegmentCount} segment files for task {TaskId}")]
    public static partial void LogMergingSegments(
        this ILogger logger,
        int segmentCount,
        Guid taskId);

    [LoggerMessage(
        EventId = 4027,
        Level = LogLevel.Information,
        Message = "Task {TaskId}: Download completed successfully. Final path: {FinalPath}")]
    public static partial void LogDownloadCompleted(
        this ILogger logger,
        Guid taskId,
        string finalPath);

    [LoggerMessage(
        EventId = 4028,
        Level = LogLevel.Information,
        Message = "Task {TaskId}: Download paused by user")]
    public static partial void LogDownloadPaused(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 4029,
        Level = LogLevel.Error,
        Message = "Task {TaskId}: Download failed")]
    public static partial void LogDownloadFailed(
        this ILogger logger,
        Exception exception,
        Guid taskId);

    // Resume operations (4050-4069)
    [LoggerMessage(
        EventId = 4050,
        Level = LogLevel.Information,
        Message = "Resuming download for task {TaskId}")]
    public static partial void LogDownloadResuming(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 4051,
        Level = LogLevel.Error,
        Message = "Task {TaskId}: Cannot resume - segment configuration or temp file path not found")]
    public static partial void LogResumeConfigurationMissing(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 4052,
        Level = LogLevel.Information,
        Message = "Merging {SegmentCount} segment files for resumed task {TaskId}")]
    public static partial void LogMergingResumedSegments(
        this ILogger logger,
        int segmentCount,
        Guid taskId);

    [LoggerMessage(
        EventId = 4053,
        Level = LogLevel.Information,
        Message = "Task {TaskId}: Resumed download completed successfully. Final path: {FinalPath}")]
    public static partial void LogResumedDownloadCompleted(
        this ILogger logger,
        Guid taskId,
        string finalPath);

    [LoggerMessage(
        EventId = 4054,
        Level = LogLevel.Error,
        Message = "Task {TaskId}: Resume failed")]
    public static partial void LogResumeFailed(
        this ILogger logger,
        Exception exception,
        Guid taskId);

    // Resume validation (4070-4079)
    [LoggerMessage(
        EventId = 4070,
        Level = LogLevel.Warning,
        Message = "Task {TaskId}: Cannot resume - metadata is missing")]
    public static partial void LogResumeMetadataMissing(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 4071,
        Level = LogLevel.Warning,
        Message = "Task {TaskId}: Cannot resume - server does not support range requests")]
    public static partial void LogResumeRangeNotSupported(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 4072,
        Level = LogLevel.Warning,
        Message = "Task {TaskId}: Cannot resume - file has changed on server (ETag mismatch). Expected: {ExpectedETag}, Current: {CurrentETag}")]
    public static partial void LogResumeETagMismatch(
        this ILogger logger,
        Guid taskId,
        string expectedETag,
        string currentETag);

    [LoggerMessage(
        EventId = 4073,
        Level = LogLevel.Warning,
        Message = "Task {TaskId}: Cannot resume - file has changed on server (Last-Modified mismatch)")]
    public static partial void LogResumeLastModifiedMismatch(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 4074,
        Level = LogLevel.Warning,
        Message = "Task {TaskId}: Cannot resume - file size has changed. Expected: {Expected} bytes, Current: {Current} bytes")]
    public static partial void LogResumeFileSizeMismatch(
        this ILogger logger,
        Guid taskId,
        long expected,
        long current);

    // State persistence (4080-4099)
    [LoggerMessage(
        EventId = 4080,
        Level = LogLevel.Debug,
        Message = "Saving state for task {TaskId}")]
    public static partial void LogSavingTaskState(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 4081,
        Level = LogLevel.Error,
        Message = "Failed to save state for task {TaskId}")]
    public static partial void LogSaveStateFailed(
        this ILogger logger,
        Exception exception,
        Guid taskId);

    [LoggerMessage(
        EventId = 4082,
        Level = LogLevel.Information,
        Message = "Recovering persisted download states...")]
    public static partial void LogRecoveringStates(
        this ILogger logger);

    [LoggerMessage(
        EventId = 4083,
        Level = LogLevel.Information,
        Message = "Successfully recovered {Count} persisted download states")]
    public static partial void LogStatesRecovered(
        this ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 4084,
        Level = LogLevel.Warning,
        Message = "Failed to recover state for task {TaskId}")]
    public static partial void LogRecoverTaskStateFailed(
        this ILogger logger,
        Exception exception,
        Guid taskId);

    [LoggerMessage(
        EventId = 4085,
        Level = LogLevel.Error,
        Message = "Failed to recover persisted states")]
    public static partial void LogRecoverStatesFailed(
        this ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 4086,
        Level = LogLevel.Information,
        Message = "Task {TaskId}: Restored from persisted state. State: {State}, Progress: {Progress}/{Total} bytes")]
    public static partial void LogTaskRestored(
        this ILogger logger,
        Guid taskId,
        string state,
        long progress,
        long total);

    // Queue operations (4090-4099)
    [LoggerMessage(
        EventId = 4090,
        Level = LogLevel.Debug,
        Message = "Task {TaskId} added to download queue with priority {Priority}")]
    public static partial void LogTaskQueued(
        this ILogger logger,
        Guid taskId,
        string priority);

    [LoggerMessage(
        EventId = 4091,
        Level = LogLevel.Debug,
        Message = "Task {TaskId} marked as started")]
    public static partial void LogTaskStarted(
        this ILogger logger,
        Guid taskId);

    [LoggerMessage(
        EventId = 4092,
        Level = LogLevel.Information,
        Message = "Pausing all active downloads. Count: {Count}")]
    public static partial void LogPausingAll(
        this ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 4093,
        Level = LogLevel.Information,
        Message = "Paused {Paused} out of {Total} active downloads")]
    public static partial void LogPausedAll(
        this ILogger logger,
        int paused,
        int total);

    [LoggerMessage(
        EventId = 4094,
        Level = LogLevel.Information,
        Message = "Clearing {Count} completed downloads")]
    public static partial void LogClearingCompleted(
        this ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 4095,
        Level = LogLevel.Information,
        Message = "Task {TaskId} cancelled. RemovePartialFiles: {RemoveFiles}")]
    public static partial void LogTaskCancelled(
        this ILogger logger,
        Guid taskId,
        bool removeFiles);
}
