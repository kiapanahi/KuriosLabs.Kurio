namespace KuriousLabs.Kurio.Core.Resilience;

/// <summary>
///     Configuration options for resilience policies.
/// </summary>
public sealed class ResiliencePolicyOptions
{
    /// <summary>
    ///     Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the initial delay in seconds before the first retry.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the backoff multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    ///     Gets or sets the circuit breaker failure threshold.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the circuit breaker duration in seconds.
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    /// <summary>
    ///     Gets or sets the timeout in seconds for download operations.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 360;

    /// <summary>
    ///     Gets or sets whether to enable jitter for retry delays.
    /// </summary>
    public bool EnableJitter { get; set; } = true;
}
