namespace Kurio.Core.Models;

/// <summary>
///     Represents the complete persisted state of a download task.
/// </summary>
public sealed class DownloadTaskState
{
    /// <summary>
    ///     Gets or sets the format version of this state file.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    ///     Gets or sets the unique identifier for this download task.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    ///     Gets or sets the source URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    ///     Gets or sets the file name.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    ///     Gets or sets the total file size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    ///     Gets or sets the destination directory.
    /// </summary>
    public required string DestinationDirectory { get; set; }

    /// <summary>
    ///     Gets or sets the temporary file path.
    /// </summary>
    public string? TempFilePath { get; set; }

    /// <summary>
    ///     Gets or sets the current state of the download.
    /// </summary>
    public DownloadState State { get; set; }

    /// <summary>
    ///     Gets or sets the download priority.
    /// </summary>
    public DownloadPriority Priority { get; set; }

    /// <summary>
    ///     Gets or sets the resource metadata.
    /// </summary>
    public ResourceMetadata? Metadata { get; set; }

    /// <summary>
    ///     Gets or sets the segment states.
    /// </summary>
    public List<SegmentState> Segments { get; set; } = [];

    /// <summary>
    ///     Gets or sets the timestamp when this download was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp when this download started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp of the last state update.
    /// </summary>
    public DateTime LastUpdateAt { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp when this download completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    ///     Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    ///     Gets or sets the last error if any.
    /// </summary>
    public DownloadError? LastError { get; set; }

    /// <summary>
    ///     Gets or sets the download options.
    /// </summary>
    public DownloadOptions? Options { get; set; }

    /// <summary>
    ///     Gets the total bytes downloaded across all segments.
    /// </summary>
    public long TotalBytesDownloaded => Segments.Sum(s => s.BytesDownloaded);

    /// <summary>
    ///     Gets the completion percentage (0-100).
    /// </summary>
    public double CompletedPercent => FileSize > 0 
        ? Math.Round((double)TotalBytesDownloaded / FileSize * 100, 2) 
        : 0;

    /// <summary>
    ///     Gets whether this download can be resumed.
    /// </summary>
    public bool CanResume => State == DownloadState.Paused &&
                             Metadata?.SupportsRanges == true &&
                             Segments.Count > 0;
}
