namespace Kurio.Core.Statistics;

/// <summary>
///     Calculates download speed using a rolling average to smooth out fluctuations.
/// </summary>
public sealed class SpeedCalculator
{
    private readonly object _lock = new();
    private readonly Queue<SpeedSample> _samples;
    private readonly int _windowSize;
    private SpeedSample? _lastSample;
    private DateTime? _pauseStartTime;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeedCalculator" /> class.
    /// </summary>
    /// <param name="windowSize">The number of samples to use for rolling average calculation.</param>
    public SpeedCalculator(int windowSize = 10)
    {
        if (windowSize <= 0)
        {
            throw new ArgumentException("Window size must be positive.", nameof(windowSize));
        }

        _windowSize = windowSize;
        _samples = new Queue<SpeedSample>(windowSize);
    }

    /// <summary>
    ///     Gets the current speed in bytes per second based on the most recent sample.
    /// </summary>
    public long CurrentSpeed { get; private set; }

    /// <summary>
    ///     Gets the rolling average speed in bytes per second.
    /// </summary>
    public long AverageSpeed { get; private set; }

    /// <summary>
    ///     Gets the peak speed observed in bytes per second.
    /// </summary>
    public long PeakSpeed { get; private set; }

    /// <summary>
    ///     Gets the total duration spent paused in milliseconds.
    /// </summary>
    public long TotalPausedDurationMs { get; private set; }

    /// <summary>
    ///     Records a new speed sample.
    /// </summary>
    /// <param name="bytesDownloaded">The total number of bytes downloaded since the start.</param>
    /// <param name="timestamp">The timestamp of this measurement.</param>
    public void RecordSample(long bytesDownloaded, DateTime timestamp)
    {
        lock (_lock)
        {
            // Don't record samples while paused
            if (_pauseStartTime.HasValue)
            {
                return;
            }

            SpeedSample sample = new(bytesDownloaded, timestamp);

            // Calculate current speed from the most recent previous sample
            if (_lastSample.HasValue)
            {
                var timeDiffMs = (timestamp - _lastSample.Value.Timestamp).TotalMilliseconds;

                if (timeDiffMs > 0)
                {
                    var bytesDiff = bytesDownloaded - _lastSample.Value.BytesDownloaded;
                    CurrentSpeed = (long)(bytesDiff / (timeDiffMs / 1000.0));

                    if (CurrentSpeed > PeakSpeed)
                    {
                        PeakSpeed = CurrentSpeed;
                    }
                }
            }

            _lastSample = sample;
            _samples.Enqueue(sample);

            // Maintain window size
            while (_samples.Count > _windowSize)
            {
                _samples.Dequeue();
            }

            // Calculate rolling average from the window
            if (_samples.Count > 1)
            {
                var oldest = _samples.Peek();
                var newest = sample;
                var totalTimeDiffMs = (newest.Timestamp - oldest.Timestamp).TotalMilliseconds;

                if (totalTimeDiffMs > 0)
                {
                    var totalBytesDiff = newest.BytesDownloaded - oldest.BytesDownloaded;
                    AverageSpeed = (long)(totalBytesDiff / (totalTimeDiffMs / 1000.0));
                }
            }
        }
    }

    /// <summary>
    ///     Marks the download as paused. Time spent paused will be excluded from speed calculations.
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            if (!_pauseStartTime.HasValue)
            {
                _pauseStartTime = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    ///     Marks the download as resumed.
    /// </summary>
    public void Resume()
    {
        lock (_lock)
        {
            if (_pauseStartTime.HasValue)
            {
                var pauseDuration = (long)(DateTime.UtcNow - _pauseStartTime.Value).TotalMilliseconds;
                TotalPausedDurationMs += pauseDuration;
                _pauseStartTime = null;
            }
        }
    }

    /// <summary>
    ///     Resets the calculator to its initial state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _samples.Clear();
            _lastSample = null;
            CurrentSpeed = 0;
            AverageSpeed = 0;
            PeakSpeed = 0;
            TotalPausedDurationMs = 0;
            _pauseStartTime = null;
        }
    }

    private readonly record struct SpeedSample(long BytesDownloaded, DateTime Timestamp);
}
