namespace Kurio.Core.Models;

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed, allowing requests through.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open, blocking all requests.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open, allowing limited test requests.
    /// </summary>
    HalfOpen
}
