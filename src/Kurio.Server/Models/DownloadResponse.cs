using Kurio.Core.Abstractions;
using Kurio.Core.Models;

namespace KuriousLabs.Kurio.Server.Models;

/// <summary>
///     Response model representing a download.
/// </summary>
public record DownloadResponse
{
    /// <summary>
    ///     Unique identifier for the download.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    ///     The URL being downloaded.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    ///     The filename of the download.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    ///     Total file size in bytes.
    /// </summary>
    public required long FileSize { get; init; }

    /// <summary>
    ///     Current state of the download.
    /// </summary>
    public required DownloadState State { get; init; }

    /// <summary>
    ///     Priority of the download in the queue.
    /// </summary>
    public required DownloadPriority Priority { get; init; }

    /// <summary>
    ///     Current progress information.
    /// </summary>
    public DownloadProgressDto? Progress { get; init; }

    /// <summary>
    ///     When the download was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    ///     When the download started (if started).
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    ///     When the download completed (if completed).
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    ///     Error message if the download failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    ///     Creates a DownloadResponse from an IDownloadTask.
    /// </summary>
    public static DownloadResponse FromTask(IDownloadTask task)
    {
        return new DownloadResponse
        {
            Id = task.Id,
            Url = task.Url.ToString(),
            FileName = task.FileName,
            FileSize = task.FileSize,
            State = task.State,
            Priority = task.Priority,
            Progress = task.State == DownloadState.Downloading
                ? new DownloadProgressDto
                {
                    BytesDownloaded = task.Progress.BytesDownloaded,
                    TotalBytes = task.FileSize,
                    PercentComplete = task.FileSize > 0
                        ? (double)task.Progress.BytesDownloaded / task.FileSize * 100
                        : 0
                }
                : null,
            CreatedAt = task.CreatedAt,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
            ErrorMessage = task.LastError?.Message
        };
    }
}

/// <summary>
///     Progress information for a download.
/// </summary>
public record DownloadProgressDto
{
    /// <summary>
    ///     Bytes downloaded so far.
    /// </summary>
    public required long BytesDownloaded { get; init; }

    /// <summary>
    ///     Total bytes to download.
    /// </summary>
    public required long TotalBytes { get; init; }

    /// <summary>
    ///     Percentage complete (0-100).
    /// </summary>
    public required double PercentComplete { get; init; }

    /// <summary>
    ///     Current download speed in bytes per second.
    /// </summary>
    public long? BytesPerSecond { get; init; }

    /// <summary>
    ///     Estimated time remaining.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    ///     Number of active connections.
    /// </summary>
    public int? ActiveConnections { get; init; }
}
