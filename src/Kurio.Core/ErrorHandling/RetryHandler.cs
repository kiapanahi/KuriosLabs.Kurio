using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;

using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.ErrorHandling;

/// <summary>
///     Implements retry logic with configurable strategies.
/// </summary>
public sealed class RetryHandler(ILogger<RetryHandler> logger) : IRetryHandler
{
    private readonly ILogger<RetryHandler> _logger = logger;

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        RetryPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        policy ??= RetryPolicy.Default;
        var attemptNumber = 0;
        Exception? lastException = null;

        while (attemptNumber <= policy.MaxRetryAttempts)
        {
            attemptNumber++;
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _logger.LogExecutingOperation(attemptNumber, policy.MaxRetryAttempts + 1);

                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attemptNumber <= policy.MaxRetryAttempts)
            {
                lastException = ex;
                _logger.LogOperationFailed(ex, attemptNumber, policy.MaxRetryAttempts + 1);

                var delay = CalculateDelay(attemptNumber, policy);
                _logger.LogRetryingAfterDelay(delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogOperationFailedFinal(lastException!, policy.MaxRetryAttempts + 1);
        throw lastException!;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        RetryPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async ct =>
        {
            await operation(ct).ConfigureAwait(false);
            return true;
        }, policy, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public TimeSpan CalculateDelay(int attemptNumber, RetryPolicy policy)
    {
        var delay = policy.Strategy switch
        {
            RetryStrategy.None => TimeSpan.Zero,
            RetryStrategy.Fixed => policy.InitialDelay,
            RetryStrategy.Linear => TimeSpan.FromMilliseconds(
                policy.InitialDelay.TotalMilliseconds * attemptNumber),
            RetryStrategy.ExponentialBackoff => TimeSpan.FromMilliseconds(
                policy.InitialDelay.TotalMilliseconds * Math.Pow(policy.BackoffMultiplier, attemptNumber - 1)),
            _ => policy.InitialDelay
        };

        // Cap at max delay
        if (delay > policy.MaxDelay)
        {
            delay = policy.MaxDelay;
        }

        // Apply jitter if enabled
        if (policy.UseJitter)
        {
            var jitter = Random.Shared.NextDouble() * 0.3; // +/- 30% jitter
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * (1 + jitter - 0.15));
        }

        return delay;
    }
}
