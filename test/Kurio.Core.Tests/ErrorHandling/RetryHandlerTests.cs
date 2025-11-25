using Kurio.Core.ErrorHandling;
using Kurio.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kurio.Core.Tests.ErrorHandling;

public class RetryHandlerTests
{
    private readonly RetryHandler _retryHandler;

    public RetryHandlerTests()
    {
        _retryHandler = new RetryHandler(NullLogger<RetryHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_SucceedsOnFirstAttempt_ReturnsResult()
    {
        // Arrange
        var expectedResult = 42;
        var policy = new RetryPolicy { MaxRetryAttempts = 3 };

        // Act
        var result = await _retryHandler.ExecuteAsync(
            _ => Task.FromResult(expectedResult),
            policy);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteAsync_FailsOnceThenSucceeds_ReturnsResult()
    {
        // Arrange
        var attemptCount = 0;
        var policy = new RetryPolicy { MaxRetryAttempts = 3, InitialDelay = TimeSpan.FromMilliseconds(10) };

        // Act
        var result = await _retryHandler.ExecuteAsync<int>(async _ =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new InvalidOperationException("First attempt fails");
            }
            return await Task.FromResult(42);
        }, policy);

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExceedsMaxRetries_ThrowsException()
    {
        // Arrange
        var attemptCount = 0;
        var policy = new RetryPolicy { MaxRetryAttempts = 2, InitialDelay = TimeSpan.FromMilliseconds(10) };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _retryHandler.ExecuteAsync<int>(_ =>
            {
                attemptCount++;
                throw new InvalidOperationException($"Attempt {attemptCount}");
            }, policy);
        });

        Assert.Equal(3, attemptCount); // Initial + 2 retries
    }

    [Fact]
    public void CalculateDelay_ExponentialBackoff_IncreasesExponentially()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.ExponentialBackoff,
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0,
            UseJitter = false
        };

        // Act
        var delay1 = _retryHandler.CalculateDelay(1, policy);
        var delay2 = _retryHandler.CalculateDelay(2, policy);
        var delay3 = _retryHandler.CalculateDelay(3, policy);

        // Assert
        Assert.Equal(1000, delay1.TotalMilliseconds);
        Assert.Equal(2000, delay2.TotalMilliseconds);
        Assert.Equal(4000, delay3.TotalMilliseconds);
    }

    [Fact]
    public void CalculateDelay_Linear_IncreasesLinearly()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.Linear,
            InitialDelay = TimeSpan.FromSeconds(1),
            UseJitter = false
        };

        // Act
        var delay1 = _retryHandler.CalculateDelay(1, policy);
        var delay2 = _retryHandler.CalculateDelay(2, policy);
        var delay3 = _retryHandler.CalculateDelay(3, policy);

        // Assert
        Assert.Equal(1000, delay1.TotalMilliseconds);
        Assert.Equal(2000, delay2.TotalMilliseconds);
        Assert.Equal(3000, delay3.TotalMilliseconds);
    }

    [Fact]
    public void CalculateDelay_Fixed_ReturnsConstantDelay()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.Fixed,
            InitialDelay = TimeSpan.FromSeconds(2),
            UseJitter = false
        };

        // Act
        var delay1 = _retryHandler.CalculateDelay(1, policy);
        var delay2 = _retryHandler.CalculateDelay(2, policy);
        var delay3 = _retryHandler.CalculateDelay(3, policy);

        // Assert
        Assert.Equal(2000, delay1.TotalMilliseconds);
        Assert.Equal(2000, delay2.TotalMilliseconds);
        Assert.Equal(2000, delay3.TotalMilliseconds);
    }

    [Fact]
    public void CalculateDelay_ExceedsMaxDelay_CapsAtMaxDelay()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.ExponentialBackoff,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(5),
            BackoffMultiplier = 2.0,
            UseJitter = false
        };

        // Act
        var delay10 = _retryHandler.CalculateDelay(10, policy);

        // Assert
        Assert.Equal(5000, delay10.TotalMilliseconds);
    }

    [Fact]
    public void CalculateDelay_WithJitter_VariesDelay()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.Fixed,
            InitialDelay = TimeSpan.FromSeconds(1),
            UseJitter = true
        };

        // Act - run multiple times to check for variation
        var delays = new List<double>();
        for (int i = 0; i < 10; i++)
        {
            var delay = _retryHandler.CalculateDelay(1, policy);
            delays.Add(delay.TotalMilliseconds);
        }

        // Assert - with jitter, we should see some variation
        var uniqueDelays = delays.Distinct().Count();
        Assert.True(uniqueDelays > 1, "Jitter should produce varied delays");

        // All delays should be within reasonable range (850-1150ms for 1000ms base with 30% jitter)
        Assert.All(delays, d => Assert.InRange(d, 850, 1150));
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_ThrowsOperationCancelledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var policy = new RetryPolicy { MaxRetryAttempts = 3 };

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await _retryHandler.ExecuteAsync<int>(async ct =>
            {
                await cts.CancelAsync();
                ct.ThrowIfCancellationRequested();
                return 42;
            }, policy, cts.Token);
        });
    }
}
