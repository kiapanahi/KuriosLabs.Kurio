using System.Collections.Concurrent;

using Kurio.Core.Abstractions;
using Kurio.Core.Models;

using Microsoft.Extensions.Logging;

namespace Kurio.Core.ErrorHandling;

/// <summary>
///     Implements the circuit breaker pattern to prevent cascading failures.
/// </summary>
public sealed class CircuitBreaker : ICircuitBreaker
{
    private readonly ConcurrentQueue<DateTime> _failureTimestamps = new();
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly CircuitBreakerPolicy _policy;
    private readonly object _stateLock = new();
    private DateTime _openedAt;

    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _successCount;

    public CircuitBreaker(CircuitBreakerPolicy policy, ILogger<CircuitBreaker> logger)
    {
        _policy = policy;
        _logger = logger;
    }

    /// <inheritdoc />
    public CircuitBreakerState State
    {
        get
        {
            lock (_stateLock)
            {
                UpdateState();
                return _state;
            }
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ThrowIfOpen();

        try
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_policy.Timeout);

            var result = await operation(cts.Token).ConfigureAwait(false);
            RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure();
            _logger.LogCircuitBreakerOperationFailed(ex, 1, 1);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async ct =>
        {
            await operation(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void RecordSuccess()
    {
        lock (_stateLock)
        {
            if (_state == CircuitBreakerState.HalfOpen)
            {
                _successCount++;
                _logger.LogSuccessCount(_successCount, _policy.SuccessThreshold);

                if (_successCount >= _policy.SuccessThreshold)
                {
                    TransitionTo(CircuitBreakerState.Closed);
                    _successCount = 0;
                    _failureTimestamps.Clear();
                    _logger.LogCircuitBreakerClosed();
                }
            }
            else if (_state == CircuitBreakerState.Closed)
            {
                // Remove old failures outside the window
                CleanupOldFailures();
            }
        }
    }

    /// <inheritdoc />
    public void RecordFailure()
    {
        lock (_stateLock)
        {
            _failureTimestamps.Enqueue(DateTime.UtcNow);
            CleanupOldFailures();

            var recentFailures = _failureTimestamps.Count;
            _logger.LogFailureCount(recentFailures, _policy.FailureThreshold);

            if (_state == CircuitBreakerState.HalfOpen)
            {
                TransitionTo(CircuitBreakerState.Open);
                _successCount = 0;
                _logger.LogCircuitBreakerOpenedFromHalfOpen();
            }
            else if (_state == CircuitBreakerState.Closed && recentFailures >= _policy.FailureThreshold)
            {
                TransitionTo(CircuitBreakerState.Open);
                _logger.LogCircuitBreakerOpened(recentFailures);
            }
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_stateLock)
        {
            TransitionTo(CircuitBreakerState.Closed);
            _failureTimestamps.Clear();
            _successCount = 0;
            _logger.LogCircuitBreakerReset();
        }
    }

    private void UpdateState()
    {
        if (_state == CircuitBreakerState.Open)
        {
            var elapsed = DateTime.UtcNow - _openedAt;
            if (elapsed >= _policy.OpenDuration)
            {
                TransitionTo(CircuitBreakerState.HalfOpen);
                _logger.LogCircuitBreakerHalfOpen(elapsed.TotalSeconds);
            }
        }
    }

    private void ThrowIfOpen()
    {
        lock (_stateLock)
        {
            UpdateState();

            if (_state == CircuitBreakerState.Open)
            {
                var elapsed = DateTime.UtcNow - _openedAt;
                var remaining = _policy.OpenDuration - elapsed;
                throw new InvalidOperationException(
                    $"Circuit breaker is open. Retry in {remaining.TotalSeconds:F0} seconds.");
            }
        }
    }

    private void TransitionTo(CircuitBreakerState newState)
    {
        var oldState = _state;
        _state = newState;

        if (newState == CircuitBreakerState.Open)
        {
            _openedAt = DateTime.UtcNow;
        }

        _logger.LogStateTransition(oldState.ToString(), newState.ToString());
    }

    private void CleanupOldFailures()
    {
        var cutoff = DateTime.UtcNow - _policy.FailureWindow;

        while (_failureTimestamps.TryPeek(out var timestamp) && timestamp < cutoff)
        {
            _failureTimestamps.TryDequeue(out _);
        }
    }
}
