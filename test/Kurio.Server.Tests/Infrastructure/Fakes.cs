using KuriousLabs.Kurio.Core.Abstractions;

using CoreModels = KuriousLabs.Kurio.Core.Models;

namespace Kurio.Server.Tests;

public sealed class FakeDownloadTask : IDownloadTask
{
    public Guid Id { get; init; }
    public required Uri Url { get; init; }
    public required string FileName { get; init; }
    public long FileSize { get; init; }
    public CoreModels.DownloadState State { get; init; }
    public CoreModels.DownloadPriority Priority { get; set; }
    public CoreModels.DownloadProgress Progress { get; init; } = new();
    public CoreModels.DownloadOptions Options { get; init; } = new() { DestinationDirectory = "downloads" };
    public CoreModels.ResourceMetadata Metadata { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public CoreModels.DownloadError? LastError { get; init; }
    public int RetryCount { get; init; }
    public CoreModels.ChecksumResult? ChecksumResult { get; init; }

    public static FakeDownloadTask Create(string name, CoreModels.DownloadPriority priority)
    {
        return new FakeDownloadTask
        {
            Id = Guid.NewGuid(),
            Url = new Uri($"https://example.com/{name}"),
            FileName = name,
            Priority = priority,
            State = CoreModels.DownloadState.Queued,
            Progress = new CoreModels.DownloadProgress { TaskId = Guid.NewGuid() },
            Options = new CoreModels.DownloadOptions { DestinationDirectory = "downloads", Category = "default", Priority = priority }
        };
    }
}

public sealed class FakeQueueManager : IDownloadQueueManager
{
    private readonly List<IDownloadTask> _queued = new();
    private int _active;

    public int MaxConcurrentDownloads { get; set; } = 3;
    public int ActiveDownloadsCount => _active;
    public int QueuedDownloadsCount => _queued.Count;

    public void Enqueue(IDownloadTask task)
    {
        _queued.Add(task);
        SortQueue();
    }

    public bool Dequeue(Guid taskId)
    {
        var removed = _queued.RemoveAll(t => t.Id == taskId) > 0;
        return removed;
    }

    public IDownloadTask? GetNextTask() => _queued.FirstOrDefault();

    public bool ChangePriority(Guid taskId, CoreModels.DownloadPriority newPriority)
    {
        var task = _queued.FirstOrDefault(t => t.Id == taskId);
        if (task is null)
        {
            return false;
        }

        task.Priority = newPriority;
        SortQueue();
        return true;
    }

    public bool MoveUp(Guid taskId)
    {
        var index = _queued.FindIndex(t => t.Id == taskId);
        if (index <= 0)
        {
            return false;
        }

        (_queued[index - 1], _queued[index]) = (_queued[index], _queued[index - 1]);
        return true;
    }

    public bool MoveDown(Guid taskId)
    {
        var index = _queued.FindIndex(t => t.Id == taskId);
        if (index < 0 || index >= _queued.Count - 1)
        {
            return false;
        }

        (_queued[index + 1], _queued[index]) = (_queued[index], _queued[index + 1]);
        return true;
    }

    public bool MoveToTop(Guid taskId)
    {
        var index = _queued.FindIndex(t => t.Id == taskId);
        if (index <= 0)
        {
            return false;
        }

        var task = _queued[index];
        _queued.RemoveAt(index);
        _queued.Insert(0, task);
        return true;
    }

    public bool MoveToBottom(Guid taskId)
    {
        var index = _queued.FindIndex(t => t.Id == taskId);
        if (index < 0)
        {
            return false;
        }

        var task = _queued[index];
        _queued.RemoveAt(index);
        _queued.Add(task);
        return true;
    }

    public IReadOnlyList<IDownloadTask> GetQueuedTasks() => _queued.ToList();
    public IReadOnlyList<IDownloadTask> GetActiveTasks() => Array.Empty<IDownloadTask>();

    public void MarkAsStarted(Guid taskId)
    {
        // Not needed for tests
    }

    public void MarkAsCompleted(Guid taskId)
    {
        // Not needed for tests
    }

    public void MarkAsFailed(Guid taskId)
    {
        // Not needed for tests
    }

    public void MarkAsPaused(Guid taskId)
    {
        // Not needed for tests
    }

    public bool CanStartNewDownload() => _active < MaxConcurrentDownloads;
    public void ClearCompleted()
    {
        // Not needed for tests
    }

    public int PauseAll()
    {
        _active = 0;
        return 0;
    }

    public IDownloadTask? GetTask(Guid taskId) => _queued.FirstOrDefault(t => t.Id == taskId);

    public void Seed(params IDownloadTask[] tasks)
    {
        _queued.Clear();
        _queued.AddRange(tasks);
        SortQueue();
    }

    public void SetCounts(int active, int queued)
    {
        _active = active;
        if (queued > _queued.Count)
        {
            var missing = queued - _queued.Count;
            for (var i = 0; i < missing; i++)
            {
                _queued.Add(FakeDownloadTask.Create($"auto-{i}", CoreModels.DownloadPriority.Normal));
            }
        }
        else if (queued < _queued.Count)
        {
            _queued.RemoveRange(queued, _queued.Count - queued);
        }
        SortQueue();
    }

    private void SortQueue()
    {
        _queued.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }
}

public sealed class FakeStatisticsService : IStatisticsService
{
    public CoreModels.DownloadStatistics Statistics { get; } = new();

    public Task<CoreModels.DownloadStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Statistics);
    }

    public Task RecordCompletedDownloadAsync(CoreModels.DownloadHistoryEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RecordFailedDownloadAsync(CoreModels.DownloadHistoryEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ResetSessionStatisticsAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IDictionary<string, object>> ExportStatisticsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>());
}

public sealed class FakeDownloadEngine : IDownloadEngine
{
    public Task<IDownloadTask> AddDownloadAsync(Uri url, CoreModels.DownloadOptions options, CancellationToken cancellationToken = default)
        => Task.FromResult<IDownloadTask>(FakeDownloadTask.Create("added", CoreModels.DownloadPriority.Normal));

    public Task CancelDownloadAsync(Guid taskId, bool removePartialFiles = false, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool ChangePriority(Guid taskId, CoreModels.DownloadPriority newPriority) => true;

    public void ClearCompleted()
    {
    }

    public IDownloadTask? GetDownload(Guid taskId) => null;

    public IEnumerable<IDownloadTask> GetDownloads(CoreModels.DownloadStateFilter filter) => Array.Empty<IDownloadTask>();

    public (int Active, int Queued) GetQueueStatistics() => (0, 0);

    public Task PauseDownloadAsync(Guid taskId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<int> PauseAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task ResumeDownloadAsync(Guid taskId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task StartDownloadAsync(Guid taskId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool MoveDown(Guid taskId) => true;
    public bool MoveUp(Guid taskId) => true;
    public bool MoveToTop(Guid taskId) => true;
    public bool MoveToBottom(Guid taskId) => true;
    public IAsyncEnumerable<CoreModels.DownloadProgress> StreamProgressAsync(Guid? taskId = null, CancellationToken cancellationToken = default)
        => AsyncEnumerable.Empty<CoreModels.DownloadProgress>();
}
