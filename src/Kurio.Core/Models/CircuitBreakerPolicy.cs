namespace Kurio.Core.Models;

/// <summary>
///     Configuration for circuit breaker behavior.
/// </summary>
public sealed class CircuitBreakerPolicy
{
    /// <summary>
    ///     Gets or sets the failure threshold before opening the circuit.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the time window for counting failures.
    /// </summary>
    public TimeSpan FailureWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     Gets or sets how long the circuit stays open before testing recovery.
    /// </summary>
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Gets or sets the number of successful requests needed to close the circuit.
    /// </summary>
    public int SuccessThreshold { get; set; } = 2;

    /// <summary>
    ///     Gets or sets the timeout for individual requests.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Gets the default circuit breaker policy.
    /// </summary>
    public static CircuitBreakerPolicy Default => new();
}
