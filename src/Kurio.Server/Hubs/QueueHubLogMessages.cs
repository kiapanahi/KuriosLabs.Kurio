using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Server.Hubs;

internal static partial class QueueHubLogMessages
{
    [LoggerMessage(
        EventId = 8300,
        Level = LogLevel.Information,
        Message = "Client {ConnectionId} subscribed to queue")]
    public static partial void LogClientSubscribedToQueue(
        this ILogger logger,
        string connectionId);

    [LoggerMessage(
        EventId = 8301,
        Level = LogLevel.Information,
        Message = "Client {ConnectionId} unsubscribed from queue")]
    public static partial void LogClientUnsubscribedFromQueue(
        this ILogger logger,
        string connectionId);

    [LoggerMessage(
        EventId = 8302,
        Level = LogLevel.Information,
        Message = "Client {ConnectionId} requested queue snapshot")]
    public static partial void LogClientRequestedQueueSnapshot(
        this ILogger logger,
        string connectionId);
}
