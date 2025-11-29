using Microsoft.Extensions.Logging;

namespace Kurio.Core.ErrorHandling;

internal static partial class RetryHandlerLogMessages
{
    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Debug,
        Message = "Executing operation, attempt {Attempt}/{MaxAttempts}")]
    public static partial void LogExecutingOperation(
        this ILogger logger,
        int attempt,
        int maxAttempts);

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Warning,
        Message = "Operation failed on attempt {Attempt}/{MaxAttempts}")]
    public static partial void LogOperationFailed(
        this ILogger logger,
        Exception exception,
        int attempt,
        int maxAttempts);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Debug,
        Message = "Retrying after {Delay}ms")]
    public static partial void LogRetryingAfterDelay(
        this ILogger logger,
        double delay);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Error,
        Message = "Operation failed after {MaxAttempts} attempts")]
    public static partial void LogOperationFailedFinal(
        this ILogger logger,
        Exception exception,
        int maxAttempts);
}
