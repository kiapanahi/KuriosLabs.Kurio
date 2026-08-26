using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Server.Controllers;

internal static partial class ConfigurationControllerLogMessages
{
    [LoggerMessage(
        EventId = 8100,
        Level = LogLevel.Information,
        Message = "Speed limit updated: Enabled={Enabled}, DownloadSpeed={DownloadSpeed} B/s, UploadSpeed={UploadSpeed} B/s (applied immediately to active downloads)")]
    public static partial void LogSpeedLimitUpdated(
        this ILogger logger,
        bool enabled,
        long downloadSpeed,
        long uploadSpeed);

    [LoggerMessage(
        EventId = 8101,
        Level = LogLevel.Error,
        Message = "Failed to update speed limit configuration")]
    public static partial void LogSpeedLimitUpdateFailed(
        this ILogger logger,
        Exception exception);
}
