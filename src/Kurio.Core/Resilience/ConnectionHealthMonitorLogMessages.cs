using System.Net;

using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.Resilience;

internal static partial class ConnectionHealthMonitorLogMessages
{
    [LoggerMessage(
        EventId = 9000,
        Level = LogLevel.Information,
        Message = "Connection monitoring is disabled")]
    public static partial void LogMonitoringDisabled(
        this ILogger logger);

    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Warning,
        Message = "Connection monitoring is already running")]
    public static partial void LogMonitoringAlreadyRunning(
        this ILogger logger);

    [LoggerMessage(
        EventId = 9002,
        Level = LogLevel.Information,
        Message = "Starting connection health monitoring (interval: {Interval}s)")]
    public static partial void LogMonitoringStarting(
        this ILogger logger,
        int interval);

    [LoggerMessage(
        EventId = 9003,
        Level = LogLevel.Information,
        Message = "Stopping connection health monitoring")]
    public static partial void LogMonitoringStopping(
        this ILogger logger);

    [LoggerMessage(
        EventId = 9004,
        Level = LogLevel.Debug,
        Message = "Checking connection health via {Endpoint}")]
    public static partial void LogCheckingHealthViaEndpoint(
        this ILogger logger,
        string endpoint);

    [LoggerMessage(
        EventId = 9005,
        Level = LogLevel.Debug,
        Message = "Health check succeeded via {Endpoint}")]
    public static partial void LogHealthCheckSucceededViaEndpoint(
        this ILogger logger,
        string endpoint);

    [LoggerMessage(
        EventId = 9006,
        Level = LogLevel.Debug,
        Message = "Health check failed via {Endpoint}: {StatusCode}")]
    public static partial void LogHealthCheckFailedViaEndpointWithStatus(
        this ILogger logger,
        string endpoint,
        HttpStatusCode statusCode);

    [LoggerMessage(
        EventId = 9007,
        Level = LogLevel.Debug,
        Message = "Health check failed via {Endpoint}")]
    public static partial void LogHealthCheckFailedViaEndpoint(
        this ILogger logger,
        Exception exception,
        string endpoint);

    [LoggerMessage(
        EventId = 9008,
        Level = LogLevel.Error,
        Message = "Error during connection health monitoring")]
    public static partial void LogMonitoringError(
        this ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 9009,
        Level = LogLevel.Information,
        Message = "Connection health monitoring stopped")]
    public static partial void LogMonitoringStopped(
        this ILogger logger);

    [LoggerMessage(
        EventId = 9010,
        Level = LogLevel.Information,
        Message = "Connection restored")]
    public static partial void LogConnectionRestored(
        this ILogger logger);

    [LoggerMessage(
        EventId = 9011,
        Level = LogLevel.Warning,
        Message = "Connection lost after {Count} consecutive failures")]
    public static partial void LogConnectionLost(
        this ILogger logger,
        int count);
}
