namespace Kurio.Core.Resilience;

/// <summary>
///     Configuration options for connection resilience and network failure recovery.
/// </summary>
public sealed class ConnectionResilienceOptions
{
    /// <summary>
    ///     Gets or sets the maximum number of retry attempts for network failures.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the initial retry delay in seconds.
    /// </summary>
    public int InitialRetryDelaySeconds { get; set; } = 2;

    /// <summary>
    ///     Gets or sets the maximum retry delay in seconds.
    /// </summary>
    public int MaxRetryDelaySeconds { get; set; } = 60;

    /// <summary>
    ///     Gets or sets the interval for network health checks in seconds.
    /// </summary>
    public int NetworkHealthCheckIntervalSeconds { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the timeout for detecting stalled transfers in seconds.
    /// </summary>
    public int StallDetectionTimeoutSeconds { get; set; } = 10;

    /// <summary>
    ///     Gets or sets a value indicating whether connection monitoring is enabled.
    /// </summary>
    public bool EnableConnectionMonitoring { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether adaptive backoff is enabled.
    /// </summary>
    public bool EnableAdaptiveBackoff { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether circuit breaker is enabled.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    ///     Gets or sets the health check endpoints used to verify connectivity.
    /// </summary>
    public List<string> HealthCheckEndpoints { get; set; } =
    [
        "https://www.google.com",
        "https://www.cloudflare.com",
        "https://www.microsoft.com"
    ];

    /// <summary>
    ///     Gets or sets the timeout for health check requests in seconds.
    /// </summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    /// <summary>
    ///     Gets or sets a value indicating whether to use jitter in retry delays.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    ///     Gets or sets the number of consecutive failures before considering connection unhealthy.
    /// </summary>
    public int ConsecutiveFailuresThreshold { get; set; } = 3;

    /// <summary>
    ///     Gets or sets a value indicating whether to automatically pause downloads on connection loss.
    /// </summary>
    public bool AutoPauseOnConnectionLoss { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether to automatically resume downloads on connection recovery.
    /// </summary>
    public bool AutoResumeOnConnectionRecovery { get; set; } = true;
}

