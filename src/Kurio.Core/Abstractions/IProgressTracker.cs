using Kurio.Core.Models;

namespace Kurio.Core.Abstractions;

/// <summary>
///     Provides enhanced progress tracking for downloads.
/// </summary>
public interface IProgressTracker
{
    /// <summary>
    ///     Streams progress updates. Optionally filter by task ID.
    /// </summary>
    /// <param name="taskId">Optional task ID to filter progress updates. If null, streams all progress.</param>
    /// <param name="cancellationToken">Cancellation token to stop streaming.</param>
    /// <returns>Async stream of enhanced progress updates.</returns>
    IAsyncEnumerable<EnhancedDownloadProgress> StreamProgressAsync(
        Guid? taskId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Starts tracking progress for a download task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="totalBytes">The total file size in bytes.</param>
    void StartTracking(Guid taskId, long totalBytes);

    /// <summary>
    ///     Records progress for a download task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="bytesDownloaded">The total bytes downloaded so far.</param>
    /// <param name="segmentProgress">Optional per-segment progress information.</param>
    void RecordProgress(Guid taskId, long bytesDownloaded, IReadOnlyList<SegmentProgressInfo>? segmentProgress = null);

    /// <summary>
    ///     Marks a download as paused.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    void Pause(Guid taskId);

    /// <summary>
    ///     Marks a download as resumed.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    void Resume(Guid taskId);

    /// <summary>
    ///     Stops tracking a download task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    void StopTracking(Guid taskId);

    /// <summary>
    ///     Gets the current enhanced progress for a download task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <returns>The enhanced progress information, or null if not being tracked.</returns>
    EnhancedDownloadProgress? GetProgress(Guid taskId);

}
