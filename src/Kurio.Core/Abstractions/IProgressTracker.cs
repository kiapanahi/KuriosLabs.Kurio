using Kurio.Core.Models;

namespace Kurio.Core.Abstractions;

/// <summary>
///     Provides enhanced progress tracking for downloads.
/// </summary>
public interface IProgressTracker
{
    /// <summary>
    ///     Gets an observable stream of all progress updates.
    /// </summary>
    IObservable<EnhancedDownloadProgress> AllProgressUpdates { get; }

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

    /// <summary>
    ///     Gets an observable stream of enhanced progress updates for a specific task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <returns>An observable that emits progress updates for the task.</returns>
    IObservable<EnhancedDownloadProgress> GetProgressUpdates(Guid taskId);
}
