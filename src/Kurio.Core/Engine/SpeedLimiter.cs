using System.Threading.RateLimiting;

using KuriousLabs.Kurio.Core.Abstractions;

namespace KuriousLabs.Kurio.Core.Engine;

/// <summary>
///     Implements bandwidth throttling using the standard .NET rate limiting API.
///     Uses token bucket algorithm for smooth bandwidth control.
/// </summary>
public sealed class SpeedLimiter : ISpeedLimiter, IAsyncDisposable
{
    private readonly System.Threading.Lock _lock = new();
    private long _maxBytesPerSecond;
    private TokenBucketRateLimiter? _limiter;
    private int _tokenLimit;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeedLimiter" /> class.
    /// </summary>
    /// <param name="maxBytesPerSecond">Maximum bytes per second (0 = unlimited).</param>
    public SpeedLimiter(long maxBytesPerSecond)
    {
        if (maxBytesPerSecond < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytesPerSecond), "Speed limit cannot be negative.");
        }

        _maxBytesPerSecond = maxBytesPerSecond;
        _limiter = CreateLimiter(maxBytesPerSecond, out _tokenLimit);
    }

    /// <summary>
    ///     Gets a value indicating whether speed limiting is enabled.
    /// </summary>
    public bool IsEnabled => _maxBytesPerSecond > 0;

    /// <summary>
    ///     Gets the maximum bytes per second limit.
    /// </summary>
    public long MaxBytesPerSecond => _maxBytesPerSecond;

    /// <summary>
    ///     Throttles the operation to respect the configured speed limit.
    ///     Returns immediately if speed limiting is disabled.
    /// </summary>
    /// <param name="bytesRequested">Number of bytes to be transferred.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the throttling delay.</returns>
    public async Task ThrottleAsync(int bytesRequested, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || bytesRequested <= 0)
        {
            return;
        }

        TokenBucketRateLimiter? limiter;
        int tokenLimit;
        lock (_lock)
        {
            limiter = _limiter;
            tokenLimit = _tokenLimit;
        }

        if (limiter == null)
        {
            return;
        }

        // TokenBucketRateLimiter enforces a per-request token cap. Chunk requests to avoid ArgumentOutOfRangeException.
        var remainingBytes = (long)bytesRequested;
        var maxTokensPerRequest = tokenLimit > 0 ? tokenLimit : int.MaxValue;

        while (remainingBytes > 0)
        {
            var toThrottle = (int)Math.Min(remainingBytes, maxTokensPerRequest);
            using var lease = await limiter.AcquireAsync(toThrottle, cancellationToken).ConfigureAwait(false);
            remainingBytes -= toThrottle;
        }
    }

    /// <summary>
    ///     Updates the maximum speed limit. Can be called at runtime.
    /// </summary>
    /// <param name="newMaxBytesPerSecond">New maximum bytes per second (0 = unlimited).</param>
    public void UpdateMaxSpeed(long newMaxBytesPerSecond)
    {
        if (newMaxBytesPerSecond < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newMaxBytesPerSecond), "Speed limit cannot be negative.");
        }

        lock (_lock)
        {
            _maxBytesPerSecond = newMaxBytesPerSecond;
            _limiter?.Dispose();
            _limiter = CreateLimiter(newMaxBytesPerSecond, out _tokenLimit);
        }
    }

    /// <summary>
    ///     Disposes the rate limiter.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            _limiter?.Dispose();
            _limiter = null;
            _tokenLimit = 0;
        }

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    private static TokenBucketRateLimiter? CreateLimiter(long maxBytesPerSecond, out int tokenLimit)
    {
        // Disabled state - no rate limiter
        if (maxBytesPerSecond <= 0)
        {
            tokenLimit = 0;
            return null;
        }

        // Replenish every 50ms: tokens per period = (maxBytesPerSecond / 1000) * 50
        // This means we refill twice as often as we used to, creating more granular throttling
        const long replenishMilliseconds = 50;
        var tokensPerPeriod = Math.Max(1, (maxBytesPerSecond * replenishMilliseconds) / 1000);
        
        // TokenLimit: Set to the refill amount per period
        // This means we can only transfer one period's worth of data before waiting for refill
        // Much tighter throttling = more predictable behavior for tests
        var bucketSize = tokensPerPeriod;

        tokenLimit = (int)Math.Min(bucketSize, int.MaxValue);

        var options = new TokenBucketRateLimiterOptions
        {
            // TokenLimit = max tokens available at any time; cap at int.MaxValue
            TokenLimit = tokenLimit,
            TokensPerPeriod = (int)Math.Min(tokensPerPeriod, int.MaxValue),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue, // Allow backpressure instead of failing requests
            ReplenishmentPeriod = TimeSpan.FromMilliseconds(replenishMilliseconds)
        };

        return new TokenBucketRateLimiter(options);
    }
}
