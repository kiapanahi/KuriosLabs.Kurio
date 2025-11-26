using Kurio.Core.Models;

namespace Kurio.Core.Abstractions;

/// <summary>
///     Represents an individual download task.
/// </summary>
public interface IDownloadTask
{
    /// <summary>
    ///     Gets the unique identifier for this download task.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    ///     Gets the URL of the resource to download.
    /// </summary>
    Uri Url { get; }

    /// <summary>
    ///     Gets the file name for the downloaded file.
    /// </summary>
    string FileName { get; }

    /// <summary>
    ///     Gets the total file size in bytes (0 if unknown).
    /// </summary>
    long FileSize { get; }

    /// <summary>
    ///     Gets the current state of the download.
    /// </summary>
    DownloadState State { get; }

    /// <summary>
    ///     Gets or sets the priority of this download.
    /// </summary>
    DownloadPriority Priority { get; set; }

    /// <summary>
    ///     Gets the current progress information.
    /// </summary>
    DownloadProgress Progress { get; }

    /// <summary>
    ///     Gets the download options.
    /// </summary>
    DownloadOptions Options { get; }

    /// <summary>
    ///     Gets the resource metadata.
    /// </summary>
    ResourceMetadata Metadata { get; }

    /// <summary>
    ///     Gets the timestamp when this task was created.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    ///     Gets the timestamp when this task started downloading.
    /// </summary>
    DateTime? StartedAt { get; }

    /// <summary>
    ///     Gets the timestamp when this task completed.
    /// </summary>
    DateTime? CompletedAt { get; }

    /// <summary>
    ///     Gets the last error that occurred (if any).
    /// </summary>
    DownloadError? LastError { get; }

    /// <summary>
    ///     Gets the number of retry attempts.
    /// </summary>
    int RetryCount { get; }

    /// <summary>
    ///     Gets the checksum verification result (if verification was performed).
    /// </summary>
    ChecksumResult? ChecksumResult { get; }
}
