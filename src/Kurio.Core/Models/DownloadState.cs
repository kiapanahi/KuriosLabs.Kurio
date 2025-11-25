namespace Kurio.Core.Models;

/// <summary>
/// Represents the current state of a download task.
/// </summary>
public enum DownloadState
{
    /// <summary>
    /// Download has been created but not yet queued.
    /// </summary>
    Created,

    /// <summary>
    /// Download is in the queue waiting to start.
    /// </summary>
    Queued,

    /// <summary>
    /// Analyzing download requirements (size, range support, etc.).
    /// </summary>
    Analyzing,

    /// <summary>
    /// Download is actively in progress.
    /// </summary>
    Downloading,

    /// <summary>
    /// Download has been paused by user or system.
    /// </summary>
    Paused,

    /// <summary>
    /// Download completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Download failed due to error.
    /// </summary>
    Failed,

    /// <summary>
    /// Download was cancelled by user.
    /// </summary>
    Cancelled
}
