namespace Kurio.Core.Models;

/// <summary>
///     Represents the progress of a download task.
/// </summary>
public sealed class DownloadProgress
{
    /// <summary>
    ///     Gets or sets the task ID this progress update belongs to.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    ///     Gets or sets the total bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; set; }

    /// <summary>
    ///     Gets or sets the total file size in bytes (0 if unknown).
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    ///     Gets the download percentage (0-100).
    /// </summary>
    public double Percentage => TotalBytes > 0 ? BytesDownloaded * 100.0 / TotalBytes : 0;

    /// <summary>
    ///     Gets or sets the current download speed in bytes per second.
    /// </summary>
    public long BytesPerSecond { get; set; }

    /// <summary>
    ///     Gets or sets the estimated time remaining.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    ///     Gets or sets the number of active connections/segments.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp of this progress update.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
