using Microsoft.Extensions.Logging;

namespace Kurio.Core.Statistics;

internal static partial class StatisticsServiceLogMessages
{
    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Debug,
        Message = "Loaded statistics from {Path}")]
    public static partial void LogStatisticsLoaded(
        this ILogger logger,
        string path);

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Error,
        Message = "Failed to load statistics from {Path}, starting fresh")]
    public static partial void LogStatisticsLoadFailed(
        this ILogger logger,
        Exception exception,
        string path);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Error,
        Message = "Failed to save statistics to {Path}")]
    public static partial void LogStatisticsSaveFailed(
        this ILogger logger,
        Exception exception,
        string path);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Debug,
        Message = "Recorded completed download statistics for {FileName}")]
    public static partial void LogCompletedDownloadRecorded(
        this ILogger logger,
        string fileName);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Debug,
        Message = "Recorded failed download statistics for {FileName}")]
    public static partial void LogFailedDownloadRecorded(
        this ILogger logger,
        string fileName);

    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Information,
        Message = "Reset session statistics")]
    public static partial void LogSessionStatisticsReset(
        this ILogger logger);

    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Information,
        Message = "Created statistics directory at {Directory}")]
    public static partial void LogStatisticsDirectoryCreated(
        this ILogger logger,
        string directory);

    [LoggerMessage(
        EventId = 6007,
        Level = LogLevel.Error,
        Message = "Failed to create statistics directory at {Directory}")]
    public static partial void LogStatisticsDirectoryCreationFailed(
        this ILogger logger,
        Exception exception,
        string directory);
}
