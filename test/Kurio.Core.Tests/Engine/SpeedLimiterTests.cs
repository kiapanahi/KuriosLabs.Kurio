using KuriousLabs.Kurio.Core.Engine;

namespace KuriousLabs.Kurio.Core.Tests.Engine;

public class SpeedLimiterTests
{
    [Fact]
    public void Constructor_WithNegativeSpeed_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpeedLimiter(-1));
    }

    [Fact]
    public void Constructor_WithZeroSpeed_CreatesDisabledLimiter()
    {
        // Arrange & Act
        var limiter = new SpeedLimiter(0);

        // Assert
        Assert.False(limiter.IsEnabled);
        Assert.Equal(0, limiter.MaxBytesPerSecond);
    }

    [Fact]
    public void Constructor_WithPositiveSpeed_CreatesEnabledLimiter()
    {
        // Arrange & Act
        var limiter = new SpeedLimiter(1_048_576); // 1 MB/s

        // Assert
        Assert.True(limiter.IsEnabled);
        Assert.Equal(1_048_576, limiter.MaxBytesPerSecond);
    }

    [Fact]
    public async Task ThrottleAsync_WhenDisabled_ReturnsImmediately()
    {
        // Arrange
        var limiter = new SpeedLimiter(0);
        var startTime = DateTime.UtcNow;

        // Act
        await limiter.ThrottleAsync(1_000_000);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 100, "Should return immediately when disabled");
    }

    [Fact]
    public async Task ThrottleAsync_WithZeroBytes_ReturnsImmediately()
    {
        // Arrange
        var limiter = new SpeedLimiter(1_048_576);
        var startTime = DateTime.UtcNow;

        // Act
        await limiter.ThrottleAsync(0);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 100, "Should return immediately with zero bytes");
    }

    [Fact]
    public async Task ThrottleAsync_WithSmallRequest_AllowsImmediateTransfer()
    {
        // Arrange
        var limiter = new SpeedLimiter(1_048_576); // 1 MB/s
        var startTime = DateTime.UtcNow;

        // Act - Request 8 KB (well below the limit)
        await limiter.ThrottleAsync(8192);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 100, "Small requests should not be throttled");
    }

    [Fact]
    public async Task ThrottleAsync_WithLargeRequest_AppliesThrottling()
    {
        // Arrange
        var limiter = new SpeedLimiter(524_288); // 512 KB/s
        var startTime = DateTime.UtcNow;

        // Act - Request 512 KB (equal to the limit)
        await limiter.ThrottleAsync(524_288);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 100, "First request should use available tokens");

        // Act - Second request should be throttled
        startTime = DateTime.UtcNow;
        await limiter.ThrottleAsync(524_288);
        elapsed = DateTime.UtcNow - startTime;

        // Assert - Should take approximately 1 second
        Assert.True(elapsed.TotalMilliseconds >= 900, "Second request should be throttled");
    }

    [Fact]
    public async Task ThrottleAsync_MultipleCalls_MaintainsAverageRate()
    {
        // Arrange
        var limiter = new SpeedLimiter(1_048_576); // 1 MB/s
        const int chunkSize = 8192; // 8 KB
        const int numChunks = 256; // Total: 2 MB (exceeds initial bucket of 1 MB)
        var startTime = DateTime.UtcNow;

        // Act
        for (var i = 0; i < numChunks; i++)
        {
            await limiter.ThrottleAsync(chunkSize);
        }

        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Should throttle for the data exceeding initial bucket
        // Bucket refills during transfer, so ~0.6s is reasonable for 2 MB
        Assert.True(elapsed.TotalSeconds >= 0.5,
            $"Expected at least 0.5 seconds for 2 MB transfer with throttling, got {elapsed.TotalSeconds} seconds");
    }

    [Fact]
    public async Task ThrottleAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var limiter = new SpeedLimiter(1024); // Very low limit
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel after 100ms

        // Act & Assert - TaskCanceledException derives from OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            // Request large amount that would require significant delay
            await limiter.ThrottleAsync(1_048_576, cts.Token);
        });
        Assert.True(exception is TaskCanceledException);
    }

    [Fact]
    public async Task ThrottleAsync_ConcurrentCalls_AreThreadSafe()
    {
        // Arrange
        var limiter = new SpeedLimiter(1_048_576); // 1 MB/s
        const int chunkSize = 8192; // 8 KB
        const int numTasks = 10;
        const int chunksPerTask = 20; // Increased to exceed bucket
        var startTime = DateTime.UtcNow;

        // Act
        var tasks = Enumerable.Range(0, numTasks)
            .Select(async _ =>
            {
                for (var i = 0; i < chunksPerTask; i++)
                {
                    await limiter.ThrottleAsync(chunkSize);
                }
            });

        await Task.WhenAll(tasks);
        var elapsed = DateTime.UtcNow - startTime;

        // Total data: 10 * 20 * 8KB = 1.6 MB
        // Should throttle concurrently without race conditions
        Assert.True(elapsed.TotalSeconds >= 0.1,
            $"Expected throttling delay for 1.6 MB concurrent transfer, got {elapsed.TotalSeconds} seconds");
    }
}
