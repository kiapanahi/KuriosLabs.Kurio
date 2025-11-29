using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Avalonia;

internal static partial class AppLogMessages
{
    [LoggerMessage(
        EventId = 9000,
        Level = LogLevel.Warning,
        Message = "Failed to connect to server on startup")]
    public static partial void LogServerConnectionFailed(
        this ILogger logger,
        Exception exception);
}
