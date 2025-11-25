using Kurio.Core.Models;

namespace Kurio.Core.Abstractions;

/// <summary>
/// Manages circuit breaker state for a resource.
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>
    /// Gets the current state of the circuit breaker.
    /// </summary>
    CircuitBreakerState State { get; }

    /// <summary>
    /// Executes an operation through the circuit breaker.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the circuit is open.</exception>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation through the circuit breaker.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the circuit is open.</exception>
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful operation.
    /// </summary>
    void RecordSuccess();

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    void RecordFailure();

    /// <summary>
    /// Resets the circuit breaker to closed state.
    /// </summary>
    void Reset();
}
