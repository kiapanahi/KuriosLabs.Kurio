namespace Kurio.Core.Models;

/// <summary>
/// Represents enhanced progress information for a download task with additional metrics.
/// </summary>
public sealed class EnhancedDownloadProgress
{
    /// <summary>
    /// Gets or sets the unique identifier of the download task.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// Gets or sets the total bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; set; }

    /// <summary>
    /// Gets or sets the total file size in bytes (0 if unknown).
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets the download percentage (0-100).
    /// </summary>
    public double Percentage => TotalBytes > 0 ? (BytesDownloaded * 100.0 / TotalBytes) : 0;

    /// <summary>
    /// Gets or sets the current download speed in bytes per second.
    /// </summary>
    public long CurrentSpeed { get; set; }

    /// <summary>
    /// Gets or sets the rolling average download speed in bytes per second.
    /// </summary>
    public long AverageSpeed { get; set; }

    /// <summary>
    /// Gets or sets the peak download speed observed in bytes per second.
    /// </summary>
    public long PeakSpeed { get; set; }

    /// <summary>
    /// Gets or sets the estimated time remaining based on current speed.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// Gets or sets the estimated time remaining based on average speed.
    /// </summary>
    public TimeSpan? EstimatedTimeRemainingAverage { get; set; }

    /// <summary>
    /// Gets or sets the number of active connections/segments.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Gets or sets the per-segment progress information.
    /// </summary>
    public IReadOnlyList<SegmentProgressInfo> SegmentProgress { get; set; } = [];

    /// <summary>
    /// Gets or sets the timestamp of this progress update.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the total time the download has been paused.
    /// </summary>
    public TimeSpan TotalPausedTime { get; set; }

    /// <summary>
    /// Gets or sets the elapsed active download time (excluding paused time).
    /// </summary>
    public TimeSpan ElapsedActiveTime { get; set; }
}

/// <summary>
/// Represents progress information for an individual segment.
/// </summary>
public sealed class SegmentProgressInfo
{
    /// <summary>
    /// Gets or sets the segment index.
    /// </summary>
    public int SegmentIndex { get; set; }

    /// <summary>
    /// Gets or sets the start byte of this segment.
    /// </summary>
    public long StartByte { get; set; }

    /// <summary>
    /// Gets or sets the end byte of this segment.
    /// </summary>
    public long EndByte { get; set; }

    /// <summary>
    /// Gets or sets the number of bytes downloaded for this segment.
    /// </summary>
    public long BytesDownloaded { get; set; }

    /// <summary>
    /// Gets the total size of this segment in bytes.
    /// </summary>
    public long TotalBytes => EndByte - StartByte + 1;

    /// <summary>
    /// Gets the completion percentage for this segment (0-100).
    /// </summary>
    public double Percentage => TotalBytes > 0 ? (BytesDownloaded * 100.0 / TotalBytes) : 0;

    /// <summary>
    /// Gets or sets whether this segment is currently being downloaded.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets whether this segment has completed.
    /// </summary>
    public bool IsCompleted { get; set; }
}
