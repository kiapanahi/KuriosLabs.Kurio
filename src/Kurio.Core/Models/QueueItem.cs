using KuriousLabs.Kurio.Core.Abstractions;

namespace KuriousLabs.Kurio.Core.Models;

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

internal sealed class QueueItemComparer : IComparer<QueueItem>
{
    public int Compare(QueueItem? x, QueueItem? y)
    {
        if (x is null && y is null) return 0;
        if (x is null) return 1;
        if (y is null) return -1;
        // First compare by priority (higher priority first)
        int priorityComparison = y.Priority.CompareTo(x.Priority);
        if (priorityComparison != 0)
        {
            return priorityComparison;
        }
        // If priorities are equal, compare by sequence number (lower sequence first)
        return x.Sequence.CompareTo(y.Sequence);
    }
}
