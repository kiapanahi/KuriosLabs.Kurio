using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.Resilience;

internal static partial class ResiliencePolicyFactoryLogMessages
{
    [LoggerMessage(
        EventId = 9100,
        Level = LogLevel.Warning,
        Message = "Retry {RetryCount}/{MaxRetries} after {Delay}s due to {Exception}")]
    public static partial void LogRetryAttempt(
        this ILogger logger,
        int retryCount,
        int maxRetries,
        double delay,
        string exception);

    [LoggerMessage(
        EventId = 9101,
        Level = LogLevel.Warning,
        Message = "Network retry {RetryCount}/{MaxRetries} after {Delay}s - Error type: {ErrorType}, Exception: {Exception}")]
    public static partial void LogNetworkRetryAttempt(
        this ILogger logger,
        int retryCount,
        int maxRetries,
        double delay,
        string errorType,
        string exception);

    [LoggerMessage(
        EventId = 9102,
        Level = LogLevel.Warning,
        Message = "Circuit breaker opened for {Duration}s due to {Exception}")]
    public static partial void LogCircuitBreakerOpenedWithException(
        this ILogger logger,
        double duration,
        string exception);

    [LoggerMessage(
        EventId = 9103,
        Level = LogLevel.Information,
        Message = "Circuit breaker reset")]
    public static partial void LogCircuitBreakerReset(
        this ILogger logger);

    [LoggerMessage(
        EventId = 9104,
        Level = LogLevel.Information,
        Message = "Circuit breaker is half-open, testing connection")]
    public static partial void LogCircuitBreakerHalfOpen(
        this ILogger logger);

    [LoggerMessage(
        EventId = 9105,
        Level = LogLevel.Warning,
        Message = "Operation timed out after {Timeout}s")]
    public static partial void LogOperationTimedOut(
        this ILogger logger,
        double timeout);

    [LoggerMessage(
        EventId = 9106,
        Level = LogLevel.Warning,
        Message = "Circuit breaker opened for {Duration}s")]
    public static partial void LogCircuitBreakerOpened(
        this ILogger logger,
        double duration);

    [LoggerMessage(
        EventId = 9107,
        Level = LogLevel.Warning,
        Message = "Retry {RetryCount}/{MaxRetries} after {Delay}s")]
    public static partial void LogRetryAttemptSimple(
        this ILogger logger,
        int retryCount,
        int maxRetries,
        double delay);
}
