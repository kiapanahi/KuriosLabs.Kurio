namespace Kurio.Core.Models;

/// <summary>
///     Represents a historical record of a completed or failed download.
/// </summary>
public sealed class DownloadHistoryEntry
{
    /// <summary>
    ///     Gets or sets the unique identifier of the download.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     Gets or sets the source URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    ///     Gets or sets the file name.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    ///     Gets or sets the file size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    ///     Gets or sets the destination directory.
    /// </summary>
    public required string DestinationDirectory { get; set; }

    /// <summary>
    ///     Gets or sets the final destination path (for completed downloads).
    /// </summary>
    public string? FinalPath { get; set; }

    /// <summary>
    ///     Gets or sets when the download was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     Gets or sets when the download started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    ///     Gets or sets when the download completed or failed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    ///     Gets or sets the total duration of the download (including paused time).
    /// </summary>
    public TimeSpan? TotalDuration { get; set; }

    /// <summary>
    ///     Gets or sets the active download time (excluding paused time).
    /// </summary>
    public TimeSpan? ActiveDuration { get; set; }

    /// <summary>
    ///     Gets or sets the total bytes downloaded.
    /// </summary>
    public long BytesDownloaded { get; set; }

    /// <summary>
    ///     Gets or sets the average download speed in bytes per second.
    /// </summary>
    public long AverageSpeed { get; set; }

    /// <summary>
    ///     Gets or sets the peak download speed in bytes per second.
    /// </summary>
    public long PeakSpeed { get; set; }

    /// <summary>
    ///     Gets or sets whether the download completed successfully.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    ///     Gets or sets the error message if the download failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///     Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    ///     Gets or sets the file's MIME type if known.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    ///     Gets the file extension.
    /// </summary>
    public string FileExtension => Path.GetExtension(FileName)?.ToLowerInvariant() ?? string.Empty;
}
