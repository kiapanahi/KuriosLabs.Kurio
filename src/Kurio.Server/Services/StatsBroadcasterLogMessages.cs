using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Server.Services;

internal static partial class StatsBroadcasterLogMessages
{
    [LoggerMessage(
        EventId = 8600,
        Level = LogLevel.Information,
        Message = "Stats broadcaster started")]
    public static partial void LogStatsBroadcasterStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 8601,
        Level = LogLevel.Error,
        Message = "Stats broadcast tick failed")]
    public static partial void LogStatsBroadcastTickFailed(
        this ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 8602,
        Level = LogLevel.Information,
        Message = "Stats broadcaster stopped")]
    public static partial void LogStatsBroadcasterStopped(this ILogger logger);
}
