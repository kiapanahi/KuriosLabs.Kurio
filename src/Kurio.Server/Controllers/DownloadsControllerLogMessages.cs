using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Server.Controllers;

internal static partial class DownloadsControllerLogMessages
{
    [LoggerMessage(
        EventId = 8000,
        Level = LogLevel.Error,
        Message = "Error adding download for URL: {Url}")]
    public static partial void LogAddDownloadError(
        this ILogger logger,
        Exception exception,
        string url);

    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Warning,
        Message = "Cannot start download {Id}")]
    public static partial void LogCannotStartDownload(
        this ILogger logger,
        Exception exception,
        Guid id);

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Warning,
        Message = "Download {Id} not found")]
    public static partial void LogDownloadNotFound(
        this ILogger logger,
        Exception exception,
        Guid id);

    [LoggerMessage(
        EventId = 8003,
        Level = LogLevel.Warning,
        Message = "Cannot pause download {Id}")]
    public static partial void LogCannotPauseDownload(
        this ILogger logger,
        Exception exception,
        Guid id);

    [LoggerMessage(
        EventId = 8004,
        Level = LogLevel.Warning,
        Message = "Cannot resume download {Id}")]
    public static partial void LogCannotResumeDownload(
        this ILogger logger,
        Exception exception,
        Guid id);

    [LoggerMessage(
        EventId = 8005,
        Level = LogLevel.Warning,
        Message = "Error cancelling download {Id}")]
    public static partial void LogCancelDownloadError(
        this ILogger logger,
        Exception exception,
        Guid id);
}
