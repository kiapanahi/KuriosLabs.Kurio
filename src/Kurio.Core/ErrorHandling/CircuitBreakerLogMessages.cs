using Microsoft.Extensions.Logging;

namespace Kurio.Core.ErrorHandling;

internal static partial class CircuitBreakerLogMessages
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Executing operation, attempt {Attempt}/{MaxAttempts}")]
    public static partial void LogCircuitBreakerExecutingOperation(
        this ILogger logger,
        int attempt,
        int maxAttempts);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Operation failed on attempt {Attempt}/{MaxAttempts}")]
    public static partial void LogCircuitBreakerOperationFailed(
        this ILogger logger,
        Exception exception,
        int attempt,
        int maxAttempts);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Retrying after {Delay}ms")]
    public static partial void LogCircuitBreakerRetrying(
        this ILogger logger,
        double delay);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Operation failed after {MaxAttempts} attempts")]
    public static partial void LogCircuitBreakerOperationFailedFinal(
        this ILogger logger,
        Exception exception,
        int maxAttempts);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Debug,
        Message = "Circuit breaker success count: {SuccessCount}/{Threshold}")]
    public static partial void LogSuccessCount(
        this ILogger logger,
        int successCount,
        int threshold);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Circuit breaker closed after successful recovery")]
    public static partial void LogCircuitBreakerClosed(
        this ILogger logger);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Debug,
        Message = "Circuit breaker failure count: {FailureCount}/{Threshold}")]
    public static partial void LogFailureCount(
        this ILogger logger,
        int failureCount,
        int threshold);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Warning,
        Message = "Circuit breaker opened from half-open state after failure")]
    public static partial void LogCircuitBreakerOpenedFromHalfOpen(
        this ILogger logger);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Warning,
        Message = "Circuit breaker opened after {FailureCount} failures")]
    public static partial void LogCircuitBreakerOpened(
        this ILogger logger,
        int failureCount);

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Information,
        Message = "Circuit breaker transitioned to half-open after {Duration}s")]
    public static partial void LogCircuitBreakerHalfOpen(
        this ILogger logger,
        double duration);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Information,
        Message = "Circuit breaker reset to closed state")]
    public static partial void LogCircuitBreakerReset(
        this ILogger logger);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Debug,
        Message = "Circuit breaker state transition: {OldState} -> {NewState}")]
    public static partial void LogStateTransition(
        this ILogger logger,
        string oldState,
        string newState);
}
