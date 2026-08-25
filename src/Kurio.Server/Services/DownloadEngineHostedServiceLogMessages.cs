using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Server.Services;

internal static partial class DownloadEngineHostedServiceLogMessages
{
    [LoggerMessage(
        EventId = 8700,
        Level = LogLevel.Information,
        Message = "Download engine hosted service starting")]
    public static partial void LogDownloadEngineStarting(this ILogger logger);

    [LoggerMessage(
        EventId = 8701,
        Level = LogLevel.Information,
        Message = "Download engine hosted service stopping")]
    public static partial void LogDownloadEngineStopping(this ILogger logger);

    [LoggerMessage(
        EventId = 8702,
        Level = LogLevel.Information,
        Message = "Paused {Count} downloads before shutdown")]
    public static partial void LogDownloadsPausedBeforeShutdown(
        this ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 8703,
        Level = LogLevel.Error,
        Message = "Error while pausing downloads during shutdown")]
    public static partial void LogPauseDownloadsOnShutdownError(
        this ILogger logger,
        Exception exception);
}
