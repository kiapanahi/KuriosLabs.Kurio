using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Server.Hubs;

internal static partial class StatsHubLogMessages
{
    [LoggerMessage(
        EventId = 8400,
        Level = LogLevel.Information,
        Message = "Client {ConnectionId} subscribed to stats")]
    public static partial void LogClientSubscribedToStats(
        this ILogger logger,
        string connectionId);

    [LoggerMessage(
        EventId = 8401,
        Level = LogLevel.Information,
        Message = "Client {ConnectionId} unsubscribed from stats")]
    public static partial void LogClientUnsubscribedFromStats(
        this ILogger logger,
        string connectionId);

    [LoggerMessage(
        EventId = 8402,
        Level = LogLevel.Information,
        Message = "Client {ConnectionId} requested stats snapshot")]
    public static partial void LogClientRequestedStatsSnapshot(
        this ILogger logger,
        string connectionId);
}
