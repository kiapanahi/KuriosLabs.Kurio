namespace Kurio.Core.Abstractions;

using Kurio.Core.Models;

/// <summary>
/// Main interface for the download engine, orchestrating all download operations.
/// </summary>
public interface IDownloadEngine
{
    /// <summary>
    /// Adds a new download to the queue.
    /// </summary>
    /// <param name="url">The URL of the resource to download.</param>
    /// <param name="options">Download configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created download task.</returns>
    Task<IDownloadTask> AddDownloadAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a queued download.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartDownloadAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses an active download.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PauseDownloadAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused download.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResumeDownloadAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a download and optionally removes partial files.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="removePartialFiles">Whether to remove partial download files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelDownloadAsync(
        Guid taskId,
        bool removePartialFiles = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific download task by its identifier.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <returns>The download task, or null if not found.</returns>
    IDownloadTask? GetDownload(Guid taskId);

    /// <summary>
    /// Gets all downloads matching the specified filter.
    /// </summary>
    /// <param name="filter">The state filter to apply.</param>
    /// <returns>A collection of matching download tasks.</returns>
    IEnumerable<IDownloadTask> GetDownloads(DownloadStateFilter filter);

    /// <summary>
    /// Gets an observable stream of download progress updates.
    /// </summary>
    IObservable<DownloadProgress> ProgressUpdates { get; }
}
