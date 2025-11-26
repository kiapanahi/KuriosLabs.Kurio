using System.Collections.Concurrent;

using Kurio.Core.Abstractions;
using Kurio.Core.Models;

using Microsoft.Extensions.Logging;

namespace Kurio.Core.ErrorHandling;

/// <summary>
/// Factory for creating and managing circuit breakers per host.
/// </summary>
public sealed class CircuitBreakerFactory
{
    private readonly ConcurrentDictionary<string, ICircuitBreaker> _circuitBreakers = new();
    private readonly CircuitBreakerPolicy _defaultPolicy;
    private readonly ILoggerFactory _loggerFactory;

    public CircuitBreakerFactory(CircuitBreakerPolicy? defaultPolicy, ILoggerFactory loggerFactory)
    {
        _defaultPolicy = defaultPolicy ?? CircuitBreakerPolicy.Default;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Gets or creates a circuit breaker for the specified host.
    /// </summary>
    /// <param name="host">The host name.</param>
    /// <returns>A circuit breaker instance for the host.</returns>
    public ICircuitBreaker GetOrCreate(string host)
    {
        return _circuitBreakers.GetOrAdd(host, _ =>
        {
            var logger = _loggerFactory.CreateLogger<CircuitBreaker>();
            return new CircuitBreaker(_defaultPolicy, logger);
        });
    }

    /// <summary>
    /// Resets all circuit breakers.
    /// </summary>
    public void ResetAll()
    {
        foreach (var breaker in _circuitBreakers.Values)
        {
            breaker.Reset();
        }
    }

    /// <summary>
    /// Removes the circuit breaker for a specific host.
    /// </summary>
    /// <param name="host">The host name.</param>
    public void Remove(string host)
    {
        _circuitBreakers.TryRemove(host, out _);
    }
}
