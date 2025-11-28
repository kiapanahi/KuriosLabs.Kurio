namespace Kurio.Core.Abstractions;

/// <summary>
///     Monitors internet connection health and availability.
/// </summary>
public interface IConnectionHealthMonitor : IDisposable
{
    /// <summary>
    ///     Gets a value indicating whether the connection is currently healthy.
    /// </summary>
    bool IsHealthy { get; }

    /// <summary>
    ///     Gets the last time the connection was verified as healthy.
    /// </summary>
    DateTime? LastHealthyAt { get; }

    /// <summary>
    ///     Gets the number of consecutive failures.
    /// </summary>
    int ConsecutiveFailures { get; }

    /// <summary>
    ///     Event raised when connection health status changes.
    /// </summary>
    event EventHandler<ConnectionHealthChangedEventArgs>? HealthChanged;

    /// <summary>
    ///     Starts monitoring connection health.
    /// </summary>
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stops monitoring connection health.
    /// </summary>
    Task StopMonitoringAsync();

    /// <summary>
    ///     Performs an immediate health check.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection is healthy, false otherwise.</returns>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Waits for the connection to become healthy.
    /// </summary>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection became healthy within timeout, false otherwise.</returns>
    Task<bool> WaitForHealthyConnectionAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>
///     Event arguments for connection health changes.
/// </summary>
public sealed class ConnectionHealthChangedEventArgs : EventArgs
{
    /// <summary>
    ///     Gets or sets a value indicating whether the connection is healthy.
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    ///     Gets or sets the timestamp of the status change.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    ///     Gets or sets the number of consecutive failures.
    /// </summary>
    public required int ConsecutiveFailures { get; init; }

    /// <summary>
    ///     Gets or sets the last error message, if any.
    /// </summary>
    public string? LastError { get; init; }
}

