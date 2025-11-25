namespace Kurio.Core.Models;

/// <summary>
/// Represents aggregated download statistics.
/// </summary>
public sealed class DownloadStatistics
{
    /// <summary>
    /// Gets or sets the total number of bytes downloaded in the current session.
    /// </summary>
    public long SessionBytesDownloaded { get; set; }

    /// <summary>
    /// Gets or sets the total number of bytes downloaded all-time.
    /// </summary>
    public long AllTimeBytesDownloaded { get; set; }

    /// <summary>
    /// Gets or sets the total number of completed downloads in the current session.
    /// </summary>
    public int SessionCompletedDownloads { get; set; }

    /// <summary>
    /// Gets or sets the total number of failed downloads in the current session.
    /// </summary>
    public int SessionFailedDownloads { get; set; }

    /// <summary>
    /// Gets or sets the total number of completed downloads all-time.
    /// </summary>
    public int AllTimeCompletedDownloads { get; set; }

    /// <summary>
    /// Gets or sets the total number of failed downloads all-time.
    /// </summary>
    public int AllTimeFailedDownloads { get; set; }

    /// <summary>
    /// Gets or sets the average download speed across all completed downloads (bytes per second).
    /// </summary>
    public long AverageDownloadSpeed { get; set; }

    /// <summary>
    /// Gets or sets the peak download speed ever recorded (bytes per second).
    /// </summary>
    public long PeakDownloadSpeed { get; set; }

    /// <summary>
    /// Gets or sets the total active download time.
    /// </summary>
    public TimeSpan TotalActiveDownloadTime { get; set; }

    /// <summary>
    /// Gets or sets the most downloaded file types with their counts.
    /// </summary>
    public IReadOnlyDictionary<string, int> FileTypeCounts { get; set; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets or sets the download counts by hour of day (0-23).
    /// </summary>
    public IReadOnlyDictionary<int, int> DownloadsByHour { get; set; } = new Dictionary<int, int>();

    /// <summary>
    /// Gets or sets when these statistics were last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when these statistics were first created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the session start time.
    /// </summary>
    public DateTime SessionStartedAt { get; set; } = DateTime.UtcNow;
}
