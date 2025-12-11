using System.Diagnostics;

using KuriousLabs.Kurio.Core.Abstractions;

namespace KuriousLabs.Kurio.Core.Engine;

/// <summary>
///     Implements token bucket algorithm for bandwidth throttling.
/// </summary>
public sealed class SpeedLimiter : ISpeedLimiter
{
    private readonly System.Threading.Lock _lock = new();
    private readonly long _maxBytesPerSecond;
    private long _availableTokens;
    private Stopwatch _stopwatch;

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
        _availableTokens = maxBytesPerSecond;
        _stopwatch = Stopwatch.StartNew();
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

        TimeSpan delayTime;

        lock (_lock)
        {
            // Refill tokens based on elapsed time
            var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();

            var tokensToAdd = (long)(elapsedSeconds * _maxBytesPerSecond);
            _availableTokens = Math.Min(_availableTokens + tokensToAdd, _maxBytesPerSecond);

            // Calculate delay if not enough tokens available
            if (_availableTokens < bytesRequested)
            {
                var tokensNeeded = bytesRequested - _availableTokens;
                var secondsNeeded = tokensNeeded / (double)_maxBytesPerSecond;
                delayTime = TimeSpan.FromSeconds(secondsNeeded);

                // Deduct all available tokens
                _availableTokens = 0;
            }
            else
            {
                // Enough tokens available, deduct and proceed
                _availableTokens -= bytesRequested;
                delayTime = TimeSpan.Zero;
            }
        }

        if (delayTime > TimeSpan.Zero)
        {
            await Task.Delay(delayTime, cancellationToken).ConfigureAwait(false);
        }
    }
}
