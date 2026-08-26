using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Server.Services;

internal static partial class ProgressBroadcasterLogMessages
{
    [LoggerMessage(
        EventId = 8500,
        Level = LogLevel.Information,
        Message = "Progress broadcaster started")]
    public static partial void LogProgressBroadcasterStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 8501,
        Level = LogLevel.Error,
        Message = "Error broadcasting progress for task {TaskId}")]
    public static partial void LogProgressBroadcastError(
        this ILogger logger,
        Exception exception,
        Guid taskId);

    [LoggerMessage(
        EventId = 8502,
        Level = LogLevel.Information,
        Message = "Progress broadcaster stopped")]
    public static partial void LogProgressBroadcasterStopped(this ILogger logger);

    [LoggerMessage(
        EventId = 8503,
        Level = LogLevel.Error,
        Message = "Progress broadcaster encountered an error")]
    public static partial void LogProgressBroadcasterError(
        this ILogger logger,
        Exception exception);
}
