using Kurio.Core.Models;

namespace Kurio.Core.Abstractions;

/// <summary>
///     Manages the download queue with priority ordering and concurrent execution limits.
/// </summary>
public interface IDownloadQueueManager
{
    /// <summary>
    ///     Gets the maximum number of concurrent downloads allowed.
    /// </summary>
    int MaxConcurrentDownloads { get; set; }

    /// <summary>
    ///     Gets the number of currently active downloads.
    /// </summary>
    int ActiveDownloadsCount { get; }

    /// <summary>
    ///     Gets the number of queued downloads waiting to start.
    /// </summary>
    int QueuedDownloadsCount { get; }

    /// <summary>
    ///     Adds a download task to the queue.
    /// </summary>
    /// <param name="task">The download task to enqueue.</param>
    void Enqueue(IDownloadTask task);

    /// <summary>
    ///     Removes a download task from the queue.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <returns>True if the task was removed; false if not found.</returns>
    bool Dequeue(Guid taskId);

    /// <summary>
    ///     Gets the next download task that should start based on priority and queue position.
    /// </summary>
    /// <returns>The next task to start, or null if none available.</returns>
    IDownloadTask? GetNextTask();

    /// <summary>
    ///     Changes the priority of a download task in the queue.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <param name="newPriority">The new priority level.</param>
    /// <returns>True if the priority was changed; false if task not found.</returns>
    bool ChangePriority(Guid taskId, DownloadPriority newPriority);

    /// <summary>
    ///     Moves a download task up in the queue (higher priority position).
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <returns>True if the task was moved; false if already at top or not found.</returns>
    bool MoveUp(Guid taskId);

    /// <summary>
    ///     Moves a download task down in the queue (lower priority position).
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <returns>True if the task was moved; false if already at bottom or not found.</returns>
    bool MoveDown(Guid taskId);

    /// <summary>
    ///     Moves a download task to the top of its priority group.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <returns>True if the task was moved; false if not found.</returns>
    bool MoveToTop(Guid taskId);

    /// <summary>
    ///     Moves a download task to the bottom of its priority group.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <returns>True if the task was moved; false if not found.</returns>
    bool MoveToBottom(Guid taskId);

    /// <summary>
    ///     Gets all tasks in the queue ordered by priority and position.
    /// </summary>
    /// <returns>An ordered collection of queued tasks.</returns>
    IReadOnlyList<IDownloadTask> GetQueuedTasks();

    /// <summary>
    ///     Gets all currently active (downloading) tasks.
    /// </summary>
    /// <returns>A collection of active tasks.</returns>
    IReadOnlyList<IDownloadTask> GetActiveTasks();

    /// <summary>
    ///     Marks a task as started (moved from queued to active).
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    void MarkAsStarted(Guid taskId);

    /// <summary>
    ///     Marks a task as completed (removed from active).
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    void MarkAsCompleted(Guid taskId);

    /// <summary>
    ///     Marks a task as failed (removed from active).
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    void MarkAsFailed(Guid taskId);

    /// <summary>
    ///     Marks a task as paused (removed from active, can be re-queued).
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    void MarkAsPaused(Guid taskId);

    /// <summary>
    ///     Checks if there are available slots for new downloads.
    /// </summary>
    /// <returns>True if new downloads can start; false if at max concurrent limit.</returns>
    bool CanStartNewDownload();

    /// <summary>
    ///     Clears all completed downloads from tracking.
    /// </summary>
    void ClearCompleted();

    /// <summary>
    ///     Pauses all active downloads.
    /// </summary>
    /// <returns>The number of downloads that were paused.</returns>
    int PauseAll();

    /// <summary>
    ///     Gets a task by its identifier from the queue.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <returns>The task if found in queue; null otherwise.</returns>
    IDownloadTask? GetTask(Guid taskId);
}
