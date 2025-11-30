using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Core.Abstractions;

/// <summary>
///     Handles retry logic for failed operations.
/// </summary>
public interface IRetryHandler
{
    /// <summary>
    ///     Executes an operation with retry logic.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="policy">The retry policy to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        RetryPolicy? policy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Executes an operation with retry logic.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="policy">The retry policy to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        RetryPolicy? policy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Calculates the delay before the next retry attempt.
    /// </summary>
    /// <param name="attemptNumber">The current attempt number (1-based).</param>
    /// <param name="policy">The retry policy to apply.</param>
    /// <returns>The delay duration.</returns>
    TimeSpan CalculateDelay(int attemptNumber, RetryPolicy policy);
}
