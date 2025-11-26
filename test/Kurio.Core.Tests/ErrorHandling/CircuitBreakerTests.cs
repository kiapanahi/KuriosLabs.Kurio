using Kurio.Core.ErrorHandling;
using Kurio.Core.Models;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Kurio.Core.Tests.ErrorHandling;

public class CircuitBreakerTests
{
    private CircuitBreaker CreateCircuitBreaker(CircuitBreakerPolicy? policy = null)
    {
        policy ??= new CircuitBreakerPolicy
        {
            FailureThreshold = 3,
            FailureWindow = TimeSpan.FromSeconds(10),
            OpenDuration = TimeSpan.FromMilliseconds(100),
            SuccessThreshold = 2,
            Timeout = TimeSpan.FromSeconds(5)
        };

        return new CircuitBreaker(policy, NullLogger<CircuitBreaker>.Instance);
    }

    [Fact]
    public void State_Initially_IsClosed()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();

        // Assert
        Assert.Equal(CircuitBreakerState.Closed, breaker.State);
    }

    [Fact]
    public async Task ExecuteAsync_Success_KeepsCircuitClosed()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();

        // Act
        var result = await breaker.ExecuteAsync(_ => Task.FromResult(42));

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(CircuitBreakerState.Closed, breaker.State);
    }

    [Fact]
    public async Task ExecuteAsync_FailuresExceedThreshold_OpensCircuit()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();

        // Act - fail 3 times to exceed threshold
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await breaker.ExecuteAsync<int>(_ => throw new InvalidOperationException("Test failure"));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Assert
        Assert.Equal(CircuitBreakerState.Open, breaker.State);
    }

    [Fact]
    public async Task ExecuteAsync_CircuitOpen_ThrowsInvalidOperationException()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();

        // Open the circuit by causing failures
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await breaker.ExecuteAsync<int>(_ => throw new InvalidOperationException("Test failure"));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await breaker.ExecuteAsync(_ => Task.FromResult(42));
        });

        Assert.Contains("Circuit breaker is open", ex.Message);
    }

    [Fact]
    public async Task State_AfterOpenDuration_TransitionsToHalfOpen()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();

        // Open the circuit
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await breaker.ExecuteAsync<int>(_ => throw new InvalidOperationException("Test failure"));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Act - wait for open duration to elapse
        await Task.Delay(150); // Policy has 100ms open duration

        // Assert
        Assert.Equal(CircuitBreakerState.HalfOpen, breaker.State);
    }

    [Fact]
    public async Task ExecuteAsync_HalfOpenSuccess_ClosesCircuit()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();

        // Open the circuit
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await breaker.ExecuteAsync<int>(_ => throw new InvalidOperationException("Test failure"));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Wait for half-open
        await Task.Delay(150);
        Assert.Equal(CircuitBreakerState.HalfOpen, breaker.State);

        // Act - succeed twice to meet success threshold
        await breaker.ExecuteAsync(_ => Task.FromResult(1));
        await breaker.ExecuteAsync(_ => Task.FromResult(2));

        // Assert
        Assert.Equal(CircuitBreakerState.Closed, breaker.State);
    }

    [Fact]
    public async Task ExecuteAsync_HalfOpenFailure_ReOpensCircuit()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();

        // Open the circuit
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await breaker.ExecuteAsync<int>(_ => throw new InvalidOperationException("Test failure"));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Wait for half-open
        await Task.Delay(150);
        Assert.Equal(CircuitBreakerState.HalfOpen, breaker.State);

        // Act - fail in half-open state
        try
        {
            await breaker.ExecuteAsync<int>(_ => throw new InvalidOperationException("Test failure"));
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert - should be open again
        Assert.Equal(CircuitBreakerState.Open, breaker.State);
    }

    [Fact]
    public void Reset_OpensCircuit_ClosesIt()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();

        // Open the circuit
        for (int i = 0; i < 3; i++)
        {
            try
            {
                breaker.ExecuteAsync<int>(_ => throw new InvalidOperationException("Test failure")).Wait();
            }
            catch
            {
                // Expected
            }
        }

        Assert.Equal(CircuitBreakerState.Open, breaker.State);

        // Act
        breaker.Reset();

        // Assert
        Assert.Equal(CircuitBreakerState.Closed, breaker.State);
    }

    [Fact]
    public void RecordSuccess_InClosedState_CleansUpOldFailures()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();

        // Record some failures
        breaker.RecordFailure();
        breaker.RecordFailure();

        // Act - record success should cleanup old failures
        breaker.RecordSuccess();

        // Assert - circuit should still be closed
        Assert.Equal(CircuitBreakerState.Closed, breaker.State);
    }

    [Fact]
    public void RecordFailure_WithinFailureWindow_CountsTowardsThreshold()
    {
        // Arrange
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 2,
            FailureWindow = TimeSpan.FromSeconds(1)
        };
        var breaker = CreateCircuitBreaker(policy);

        // Act
        breaker.RecordFailure();
        breaker.RecordFailure();

        // Assert
        Assert.Equal(CircuitBreakerState.Open, breaker.State);
    }
}
