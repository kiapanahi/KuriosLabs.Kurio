namespace Kurio.Core.Queue;

using System.Collections.Concurrent;
using Kurio.Core.Abstractions;
using Kurio.Core.Models;

/// <summary>
/// Manages the download queue with priority-based scheduling and concurrent execution limits.
/// </summary>
internal sealed class DownloadQueueManager : IDownloadQueueManager
{
    private readonly object _lock = new();
    private readonly List<QueueItem> _queuedItems = [];
    private readonly ConcurrentDictionary<Guid, IDownloadTask> _activeTasks = new();
    private readonly ConcurrentDictionary<Guid, IDownloadTask> _completedTasks = new();
    private long _sequenceCounter;

    /// <inheritdoc />
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <inheritdoc />
    public int ActiveDownloadsCount => _activeTasks.Count;

    /// <inheritdoc />
    public int QueuedDownloadsCount
    {
        get
        {
            lock (_lock)
            {
                return _queuedItems.Count;
            }
        }
    }

    /// <inheritdoc />
    public void Enqueue(IDownloadTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        lock (_lock)
        {
            // Check if task is already queued
            if (_queuedItems.Any(x => x.Task.Id == task.Id))
            {
                throw new InvalidOperationException($"Task {task.Id} is already in the queue.");
            }

            var queueItem = new QueueItem
            {
                Task = task,
                EnqueuedAt = DateTime.UtcNow,
                Sequence = Interlocked.Increment(ref _sequenceCounter)
            };

            _queuedItems.Add(queueItem);
            SortQueue();
        }
    }

    /// <inheritdoc />
    public bool Dequeue(Guid taskId)
    {
        lock (_lock)
        {
            var index = _queuedItems.FindIndex(x => x.Task.Id == taskId);
            if (index >= 0)
            {
                _queuedItems.RemoveAt(index);
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc />
    public IDownloadTask? GetNextTask()
    {
        lock (_lock)
        {
            if (_queuedItems.Count == 0)
            {
                return null;
            }

            // Already sorted by priority and sequence
            var nextItem = _queuedItems[0];
            _queuedItems.RemoveAt(0);
            return nextItem.Task;
        }
    }

    /// <inheritdoc />
    public bool ChangePriority(Guid taskId, DownloadPriority newPriority)
    {
        lock (_lock)
        {
            var item = _queuedItems.FirstOrDefault(x => x.Task.Id == taskId);
            if (item == null)
            {
                return false;
            }

            // Update task priority
            item.Task.Priority = newPriority;

            // Re-sort the queue
            SortQueue();
            return true;
        }
    }

    /// <inheritdoc />
    public bool MoveUp(Guid taskId)
    {
        lock (_lock)
        {
            var index = _queuedItems.FindIndex(x => x.Task.Id == taskId);
            if (index <= 0)
            {
                return false; // Already at top or not found
            }

            // Only swap within same priority group
            var currentPriority = _queuedItems[index].Priority;
            var previousPriority = _queuedItems[index - 1].Priority;

            if (currentPriority == previousPriority)
            {
                (_queuedItems[index], _queuedItems[index - 1]) = (_queuedItems[index - 1], _queuedItems[index]);
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc />
    public bool MoveDown(Guid taskId)
    {
        lock (_lock)
        {
            var index = _queuedItems.FindIndex(x => x.Task.Id == taskId);
            if (index < 0 || index >= _queuedItems.Count - 1)
            {
                return false; // At bottom or not found
            }

            // Only swap within same priority group
            var currentPriority = _queuedItems[index].Priority;
            var nextPriority = _queuedItems[index + 1].Priority;

            if (currentPriority == nextPriority)
            {
                (_queuedItems[index], _queuedItems[index + 1]) = (_queuedItems[index + 1], _queuedItems[index]);
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc />
    public bool MoveToTop(Guid taskId)
    {
        lock (_lock)
        {
            var index = _queuedItems.FindIndex(x => x.Task.Id == taskId);
            if (index <= 0)
            {
                return false; // Already at top or not found
            }

            var item = _queuedItems[index];
            var priority = item.Priority;

            // Find the first index with the same priority
            var topIndex = _queuedItems.FindIndex(x => x.Priority == priority);

            if (topIndex == index)
            {
                return false; // Already at top of priority group
            }

            _queuedItems.RemoveAt(index);
            _queuedItems.Insert(topIndex, item);
            return true;
        }
    }

    /// <inheritdoc />
    public bool MoveToBottom(Guid taskId)
    {
        lock (_lock)
        {
            var index = _queuedItems.FindIndex(x => x.Task.Id == taskId);
            if (index < 0)
            {
                return false; // Not found
            }

            var item = _queuedItems[index];
            var priority = item.Priority;

            // Find the last index with the same priority
            var bottomIndex = _queuedItems.FindLastIndex(x => x.Priority == priority);

            if (bottomIndex == index)
            {
                return false; // Already at bottom of priority group
            }

            _queuedItems.RemoveAt(index);
            _queuedItems.Insert(bottomIndex, item);
            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IDownloadTask> GetQueuedTasks()
    {
        lock (_lock)
        {
            return _queuedItems.Select(x => x.Task).ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IDownloadTask> GetActiveTasks()
    {
        return _activeTasks.Values.ToList();
    }

    /// <inheritdoc />
    public void MarkAsStarted(Guid taskId)
    {
        lock (_lock)
        {
            var index = _queuedItems.FindIndex(x => x.Task.Id == taskId);
            if (index >= 0)
            {
                var task = _queuedItems[index].Task;
                _queuedItems.RemoveAt(index);
                _activeTasks.TryAdd(taskId, task);
            }
        }
    }

    /// <inheritdoc />
    public void MarkAsCompleted(Guid taskId)
    {
        if (_activeTasks.TryRemove(taskId, out var task))
        {
            _completedTasks.TryAdd(taskId, task);
        }
    }

    /// <inheritdoc />
    public void MarkAsFailed(Guid taskId)
    {
        _activeTasks.TryRemove(taskId, out _);
    }

    /// <inheritdoc />
    public void MarkAsPaused(Guid taskId)
    {
        _activeTasks.TryRemove(taskId, out _);
    }

    /// <inheritdoc />
    public bool CanStartNewDownload()
    {
        return _activeTasks.Count < MaxConcurrentDownloads;
    }

    /// <inheritdoc />
    public void ClearCompleted()
    {
        _completedTasks.Clear();
    }

    /// <inheritdoc />
    public int PauseAll()
    {
        var count = _activeTasks.Count;
        _activeTasks.Clear();
        return count;
    }

    /// <inheritdoc />
    public IDownloadTask? GetTask(Guid taskId)
    {
        lock (_lock)
        {
            return _queuedItems.FirstOrDefault(x => x.Task.Id == taskId)?.Task;
        }
    }

    /// <summary>
    /// Sorts the queue by priority (descending) and sequence (ascending).
    /// </summary>
    private void SortQueue()
    {
        _queuedItems.Sort((a, b) =>
        {
            // First sort by priority (higher priority first)
            var priorityComparison = b.Priority.CompareTo(a.Priority);
            if (priorityComparison != 0)
            {
                return priorityComparison;
            }

            // Then by sequence (FIFO within same priority)
            return a.Sequence.CompareTo(b.Sequence);
        });
    }
}
