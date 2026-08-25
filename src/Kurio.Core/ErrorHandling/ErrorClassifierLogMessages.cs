using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.ErrorHandling;

internal static partial class ErrorClassifierLogMessages
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "Classified exception {ExceptionType} as {Category}")]
    public static partial void LogExceptionClassified(
        this ILogger logger,
        string exceptionType,
        string category);
}
