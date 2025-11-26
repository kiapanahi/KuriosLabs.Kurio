using Kurio.Core.Abstractions;

namespace Kurio.Core.Models;

/// <summary>
///     Represents an item in the download queue with metadata for ordering.
/// </summary>
internal sealed class QueueItem
{
    /// <summary>
    ///     Gets the download task.
    /// </summary>
    public required IDownloadTask Task { get; init; }

    /// <summary>
    ///     Gets the timestamp when the task was added to the queue.
    /// </summary>
    public DateTime EnqueuedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the sequence number for FIFO ordering within the same priority.
    /// </summary>
    public long Sequence { get; init; }

    /// <summary>
    ///     Gets the priority of this queue item.
    /// </summary>
    public DownloadPriority Priority => Task.Priority;
}
