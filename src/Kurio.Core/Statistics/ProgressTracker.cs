using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Kurio.Core.Abstractions;
using Kurio.Core.Models;

namespace Kurio.Core.Statistics;

/// <summary>
/// Provides enhanced progress tracking for downloads with speed and ETA calculations.
/// </summary>
public sealed class ProgressTracker : IProgressTracker, IDisposable
{
    private readonly ConcurrentDictionary<Guid, DownloadTrackingState> _trackingStates = new();
    private readonly Subject<EnhancedDownloadProgress> _progressSubject = new();
    private readonly int _speedWindowSize;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressTracker"/> class.
    /// </summary>
    /// <param name="speedWindowSize">The window size for rolling average speed calculation.</param>
    public ProgressTracker(int speedWindowSize = 10)
    {
        _speedWindowSize = speedWindowSize;
    }

    /// <inheritdoc />
    public IObservable<EnhancedDownloadProgress> AllProgressUpdates => _progressSubject.AsObservable();

    /// <inheritdoc />
    public void StartTracking(Guid taskId, long totalBytes)
    {
        var speedCalculator = new SpeedCalculator(_speedWindowSize);
        var etaCalculator = new EtaCalculator(speedCalculator);

        var state = new DownloadTrackingState(
            totalBytes,
            speedCalculator,
            etaCalculator,
            DateTime.UtcNow);

        _trackingStates[taskId] = state;
    }

    /// <inheritdoc />
    public void RecordProgress(Guid taskId, long bytesDownloaded, IReadOnlyList<SegmentProgressInfo>? segmentProgress = null)
    {
        if (!_trackingStates.TryGetValue(taskId, out var state))
        {
            return;
        }

        var now = DateTime.UtcNow;
        state.SpeedCalculator.RecordSample(bytesDownloaded, now);
        state.BytesDownloaded = bytesDownloaded;
        state.SegmentProgress = segmentProgress ?? [];
        state.ActiveConnections = segmentProgress?.Count(s => s.IsActive) ?? 0;

        var progress = CreateProgress(taskId, state, now);
        _progressSubject.OnNext(progress);
    }

    /// <inheritdoc />
    public void Pause(Guid taskId)
    {
        if (_trackingStates.TryGetValue(taskId, out var state))
        {
            state.SpeedCalculator.Pause();
        }
    }

    /// <inheritdoc />
    public void Resume(Guid taskId)
    {
        if (_trackingStates.TryGetValue(taskId, out var state))
        {
            state.SpeedCalculator.Resume();
        }
    }

    /// <inheritdoc />
    public void StopTracking(Guid taskId)
    {
        _trackingStates.TryRemove(taskId, out _);
    }

    /// <inheritdoc />
    public EnhancedDownloadProgress? GetProgress(Guid taskId)
    {
        if (!_trackingStates.TryGetValue(taskId, out var state))
        {
            return null;
        }

        return CreateProgress(taskId, state, DateTime.UtcNow);
    }

    /// <inheritdoc />
    public IObservable<EnhancedDownloadProgress> GetProgressUpdates(Guid taskId)
    {
        return _progressSubject.Where(p => p.TaskId == taskId);
    }

    private EnhancedDownloadProgress CreateProgress(Guid taskId, DownloadTrackingState state, DateTime now)
    {
        var bytesRemaining = state.TotalBytes - state.BytesDownloaded;
        var totalElapsed = now - state.StartTime;
        var pausedTime = TimeSpan.FromMilliseconds(state.SpeedCalculator.TotalPausedDurationMs);
        var activeTime = totalElapsed - pausedTime;

        return new EnhancedDownloadProgress
        {
            TaskId = taskId,
            BytesDownloaded = state.BytesDownloaded,
            TotalBytes = state.TotalBytes,
            CurrentSpeed = state.SpeedCalculator.CurrentSpeed,
            AverageSpeed = state.SpeedCalculator.AverageSpeed,
            PeakSpeed = state.SpeedCalculator.PeakSpeed,
            EstimatedTimeRemaining = state.EtaCalculator.GetEtaFromCurrentSpeed(bytesRemaining),
            EstimatedTimeRemainingAverage = state.EtaCalculator.GetEtaFromAverageSpeed(bytesRemaining),
            ActiveConnections = state.ActiveConnections,
            SegmentProgress = state.SegmentProgress,
            Timestamp = now,
            TotalPausedTime = pausedTime,
            ElapsedActiveTime = activeTime > TimeSpan.Zero ? activeTime : TimeSpan.Zero
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _progressSubject.Dispose();
        _trackingStates.Clear();
    }

    private sealed class DownloadTrackingState(
        long totalBytes,
        SpeedCalculator speedCalculator,
        EtaCalculator etaCalculator,
        DateTime startTime)
    {
        public long TotalBytes { get; } = totalBytes;
        public SpeedCalculator SpeedCalculator { get; } = speedCalculator;
        public EtaCalculator EtaCalculator { get; } = etaCalculator;
        public DateTime StartTime { get; } = startTime;
        public long BytesDownloaded { get; set; }
        public IReadOnlyList<SegmentProgressInfo> SegmentProgress { get; set; } = [];
        public int ActiveConnections { get; set; }
    }
}
