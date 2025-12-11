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
        // With 50ms refill: tokensPerPeriod = 512KB * 50 / 1000 = 25.6 KB
        // TokenLimit = 25.6 KB (rounded to ~25 KB)
        // So first request of 512 KB will immediately exhaust bucket and wait
        
        // Act - Request smaller amount that fits in bucket
        var startTime = DateTime.UtcNow;
        await limiter.ThrottleAsync(25_600); // 25 KB - should fit in initial bucket
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 100, "Small request within bucket should not throttle");

        // Act - Request more than bucket holds
        startTime = DateTime.UtcNow;
        await limiter.ThrottleAsync(51_200); // 50 KB - exceeds bucket, needs ~2 refills
        elapsed = DateTime.UtcNow - startTime;

        // Assert - Should take at least one refill (~50ms) for the overflow portion
        Assert.True(elapsed.TotalMilliseconds >= 40, $"Request should wait for bucket refills, got {elapsed.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task ThrottleAsync_MultipleCalls_MaintainsAverageRate()
    {
        // Arrange
        var limiter = new SpeedLimiter(1_048_576); // 1 MB/s
        // With 50ms refill: tokensPerPeriod = 1MB * 50 / 1000 = 51.2 KB (~51 KB)
        // TokenLimit = 51.2 KB per refill cycle
        const int chunkSize = 8192; // 8 KB per request
        const int numChunks = 256; // Total: 2 MB
        var startTime = DateTime.UtcNow;

        // Act
        for (var i = 0; i < numChunks; i++)
        {
            await limiter.ThrottleAsync(chunkSize);
        }

        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        // 2 MB at 1 MB/s should take ~2 seconds
        // With 50ms refill and tight bucket, we expect roughly 2 seconds
        Assert.True(elapsed.TotalSeconds >= 1.2,
            $"Expected at least 1.2 seconds for 2 MB transfer with throttling, got {elapsed.TotalSeconds} seconds");
    }

    [Fact]
    public async Task ThrottleAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var limiter = new SpeedLimiter(10240); // 10 KB/s
        // With 50ms refill: tokensPerPeriod = 10KB * 50 / 1000 = 512 B
        // TokenLimit = 512 B bucket
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // Cancel after 50ms

        // Act & Assert - TaskCanceledException derives from OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            // Request 1 MB - will need many refill cycles, should be cancelled
            await limiter.ThrottleAsync(1_048_576, cts.Token);
        });
        Assert.True(exception is TaskCanceledException);
    }

    [Fact]
    public async Task ThrottleAsync_ConcurrentCalls_AreThreadSafe()
    {
        // Arrange
        var limiter = new SpeedLimiter(1_048_576); // 1 MB/s
        // With 50ms refill: tokensPerPeriod = 1MB * 50 / 1000 = 51.2 KB
        // TokenLimit = 51.2 KB bucket, very tight for concurrent access
        const int chunkSize = 8192; // 8 KB
        const int numTasks = 10;
        const int chunksPerTask = 20; // Total 1.6 MB
        var startTime = DateTime.UtcNow;

        // Act - Multiple concurrent tasks requesting throttling
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

        // Assert
        // 1.6 MB at 1 MB/s requires at least 1.6 seconds with tight bucket
        Assert.True(elapsed.TotalSeconds >= 1.0,
            $"Expected significant throttling delay for 1.6 MB concurrent transfer, got {elapsed.TotalSeconds} seconds");
    }
}
