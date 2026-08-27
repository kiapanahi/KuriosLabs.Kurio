using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Server.Endpoints;

/// <summary>
///     Marker type supplying the log category for the configuration endpoints. Minimal-API
///     handlers live in a static class, which cannot be used as the type argument of
///     <see cref="ILogger{TCategoryName}" />, so the category is carried by this type instead.
/// </summary>
internal sealed class ConfigurationEndpointsLogCategory;

internal static partial class ConfigurationEndpointsLogMessages
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
