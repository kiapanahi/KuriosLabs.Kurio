using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KuriousLabs.Kurio.Contracts.Queue;
using KuriousLabs.Kurio.Contracts.Stats;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server;
using KuriousLabs.Kurio.Server.Controllers;
using KuriousLabs.Kurio.Server.Hubs;
using KuriousLabs.Kurio.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ContractDownloads = KuriousLabs.Kurio.Contracts.Downloads;
using CoreModels = KuriousLabs.Kurio.Core.Models;

namespace Kurio.Server.Tests;

public class QueueAndStatsTests : IClassFixture<ServerTestFactory>
{
    private readonly ServerTestFactory _factory;

    public QueueAndStatsTests(ServerTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task QueueSnapshot_ReturnsOrderedItems()
    {
        var client = _factory.WithQueue(queue =>
        {
            queue.Seed(
                FakeDownloadTask.Create("first", CoreModels.DownloadPriority.High),
                FakeDownloadTask.Create("second", CoreModels.DownloadPriority.Normal));
        }).CreateClient();

        var response = await client.GetAsync("/api/queue");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = await response.Content.ReadFromJsonAsync<List<QueueItem>>();
        snapshot.Should().NotBeNull();
        snapshot!.Count.Should().Be(2);
        snapshot[0].Position.Should().Be(1);
        snapshot[0].Priority.Should().Be(ContractDownloads.DownloadPriority.High);
        snapshot[1].Position.Should().Be(2);
        snapshot[1].Priority.Should().Be(ContractDownloads.DownloadPriority.Normal);
    }

    [Fact]
    public async Task ChangePriority_ReordersQueue()
    {
        var low = FakeDownloadTask.Create("low", CoreModels.DownloadPriority.Low);
        var normal = FakeDownloadTask.Create("normal", CoreModels.DownloadPriority.Normal);

        var client = _factory.WithQueue(queue => queue.Seed(low, normal)).CreateClient();

        var changeRequest = new ContractDownloads.ChangePriorityRequest { Priority = ContractDownloads.DownloadPriority.High };
        var response = await client.PostAsJsonAsync($"/api/queue/{low.Id}/priority", changeRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var snapshot = await client.GetFromJsonAsync<List<QueueItem>>("/api/queue");
        snapshot.Should().NotBeNull();
        snapshot!.Count.Should().Be(2);
        snapshot[0].DownloadId.Should().Be(low.Id);
        snapshot[0].Priority.Should().Be(ContractDownloads.DownloadPriority.High);
        snapshot[0].Position.Should().Be(1);
    }

    [Fact]
    public async Task StatsSnapshot_MapsStatistics()
    {
        var client = _factory.WithStats(stats =>
        {
            stats.AllTimeBytesDownloaded = 10_000;
            stats.AllTimeCompletedDownloads = 5;
            stats.AllTimeFailedDownloads = 2;
            stats.AverageDownloadSpeed = 1234;
            stats.SessionStartedAt = new DateTime(2024, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        }).WithQueue(queue => queue.SetCounts(active: 1, queued: 2)).CreateClient();

        var snapshot = await client.GetFromJsonAsync<StatsSnapshot>("/api/stats");

        snapshot.Should().NotBeNull();
        snapshot!.ActiveCount.Should().Be(1);
        snapshot.QueuedCount.Should().Be(2);
        snapshot.CompletedCount.Should().Be(5);
        snapshot.FailedCount.Should().Be(2);
        snapshot.TotalBytesDownloaded.Should().Be(10_000);
        snapshot.AverageThroughputBytesPerSecond.Should().Be(1234);
        snapshot.CurrentThroughputBytesPerSecond.Should().Be(1234);
        snapshot.StartedAt.Should().Be(new DateTimeOffset(new DateTime(2024, 12, 1, 12, 0, 0, DateTimeKind.Utc)));
    }
}

public sealed class ServerTestFactory : WebApplicationFactory<Program>
{
    private readonly FakeQueueManager _queueManager = new();
    private readonly FakeStatisticsService _statisticsService = new();

    public ServerTestFactory WithQueue(Action<FakeQueueManager> configure)
    {
        configure(_queueManager);
        return this;
    }

    public ServerTestFactory WithStats(Action<CoreModels.DownloadStatistics> configure)
    {
        configure(_statisticsService.Statistics);
        return this;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDownloadEngine>();
            services.RemoveAll<IDownloadQueueManager>();
            services.RemoveAll<IStatisticsService>();

            services.AddSingleton<IDownloadEngine, FakeDownloadEngine>();
            services.AddSingleton<IDownloadQueueManager>(_queueManager);
            services.AddSingleton<IStatisticsService>(_statisticsService);
        });

        return base.CreateHost(builder);
    }
}

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
