using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Core.Abstractions;

/// <summary>
///     Main interface for the download engine, orchestrating all download operations.
/// </summary>
public interface IDownloadEngine
{
    /// <summary>
    ///     Streams progress updates for downloads. Optionally filter by task ID.
    /// </summary>
    /// <param name="taskId">Optional task ID to filter progress updates. If null, streams all progress.</param>
    /// <param name="cancellationToken">Cancellation token to stop streaming.</param>
    /// <returns>Async stream of progress updates.</returns>
    IAsyncEnumerable<DownloadProgress> StreamProgressAsync(
        Guid? taskId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a new download to the queue.
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
    ///     Starts a queued download.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartDownloadAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Pauses an active download.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PauseDownloadAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resumes a paused download.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResumeDownloadAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cancels a download and optionally removes partial files.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="removePartialFiles">Whether to remove partial download files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelDownloadAsync(
        Guid taskId,
        bool removePartialFiles = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a specific download task by its identifier.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <returns>The download task, or null if not found.</returns>
    IDownloadTask? GetDownload(Guid taskId);

    /// <summary>
    ///     Gets all downloads matching the specified filter.
    /// </summary>
    /// <param name="filter">The state filter to apply.</param>
    /// <returns>A collection of matching download tasks.</returns>
    IEnumerable<IDownloadTask> GetDownloads(DownloadStateFilter filter);

    /// <summary>
    ///     Changes the priority of a queued download.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="newPriority">The new priority level.</param>
    /// <returns>True if the priority was changed; false if task not found or not queued.</returns>
    bool ChangePriority(Guid taskId, DownloadPriority newPriority);

    /// <summary>
    ///     Moves a queued download up in the queue.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <returns>True if the task was moved; false if not found or not queued.</returns>
    bool MoveUp(Guid taskId);

    /// <summary>
    ///     Moves a queued download down in the queue.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <returns>True if the task was moved; false if not found or not queued.</returns>
    bool MoveDown(Guid taskId);

    /// <summary>
    ///     Pauses all active downloads.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of downloads that were paused.</returns>
    Task<int> PauseAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clears all completed downloads from tracking.
    /// </summary>
    void ClearCompleted();

    /// <summary>
    ///     Gets the current queue statistics.
    /// </summary>
    /// <returns>A tuple with active and queued download counts.</returns>
    (int Active, int Queued) GetQueueStatistics();
}
