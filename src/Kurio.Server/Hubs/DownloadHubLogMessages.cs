using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Server.Hubs;

internal static partial class DownloadHubLogMessages
{
    [LoggerMessage(
        EventId = 8200,
        Level = LogLevel.Information,
        Message = "Client {ConnectionId} subscribed to downloads")]
    public static partial void LogClientSubscribedToDownloads(
        this ILogger logger,
        string connectionId);

    [LoggerMessage(
        EventId = 8201,
        Level = LogLevel.Information,
        Message = "Client {ConnectionId} unsubscribed from downloads")]
    public static partial void LogClientUnsubscribedFromDownloads(
        this ILogger logger,
        string connectionId);

    [LoggerMessage(
        EventId = 8202,
        Level = LogLevel.Information,
        Message = "Client {ConnectionId} requested snapshot")]
    public static partial void LogClientRequestedDownloadsSnapshot(
        this ILogger logger,
        string connectionId);
}
