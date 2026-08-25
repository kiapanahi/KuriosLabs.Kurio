using FluentAssertions;
using KuriousLabs.Kurio.Contracts.Downloads;
using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Contracts.Queue;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using CoreModels = KuriousLabs.Kurio.Core.Models;

namespace Kurio.Server.Tests.Hubs;

public sealed class QueueHubTests
{
    private readonly Mock<IDownloadQueueManager> _mockQueueManager;
    private readonly Mock<ILogger<QueueHub>> _mockLogger;
    private readonly Mock<IHubCallerClients<IQueueClient>> _mockClients;
    private readonly Mock<IQueueClient> _mockCaller;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly QueueHub _hub;

    public QueueHubTests()
    {
        _mockQueueManager = new Mock<IDownloadQueueManager>();
        _mockLogger = new Mock<ILogger<QueueHub>>();
        _mockClients = new Mock<IHubCallerClients<IQueueClient>>();
        _mockCaller = new Mock<IQueueClient>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _mockClients.Setup(c => c.Caller).Returns(_mockCaller.Object);
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");

        _hub = new QueueHub(_mockQueueManager.Object, _mockLogger.Object)
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    [Fact]
    public async Task SubscribeQueueAsync_AddsClientToGroup()
    {
        // Arrange
        _mockQueueManager.Setup(q => q.GetQueuedTasks()).Returns([]);

        // Act
        await _hub.SubscribeQueueAsync();

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", QueueHub.GroupName, default),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeQueueAsync_SendsQueueSnapshot()
    {
        // Arrange
        var task1 = CreateMockQueuedTask("https://example.com/file1.zip", CoreModels.DownloadPriority.High);
        var task2 = CreateMockQueuedTask("https://example.com/file2.zip", CoreModels.DownloadPriority.Normal);
        
        _mockQueueManager.Setup(q => q.GetQueuedTasks()).Returns([task1, task2]);

        // Act
        await _hub.SubscribeQueueAsync();

        // Assert
        _mockCaller.Verify(
            c => c.QueueSnapshotAsync(It.Is<List<QueueItem>>(list => 
                list.Count == 2 &&
                list[0].Position == 1 &&
                list[1].Position == 2)),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeQueueAsync_HandlesEmptyQueue()
    {
        // Arrange
        _mockQueueManager.Setup(q => q.GetQueuedTasks()).Returns([]);

        // Act
        await _hub.SubscribeQueueAsync();

        // Assert
        _mockCaller.Verify(
            c => c.QueueSnapshotAsync(It.Is<List<QueueItem>>(list => list.Count == 0)),
            Times.Once);
    }

    [Fact]
    public async Task UnsubscribeQueueAsync_RemovesClientFromGroup()
    {
        // Act
        await _hub.UnsubscribeQueueAsync();

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync("test-connection-id", QueueHub.GroupName, default),
            Times.Once);
    }

    [Fact]
    public async Task RequestQueueSnapshotAsync_SendsSnapshot()
    {
        // Arrange
        _mockQueueManager.Setup(q => q.GetQueuedTasks()).Returns([]);

        // Act
        await _hub.RequestQueueSnapshotAsync();

        // Assert
        _mockCaller.Verify(
            c => c.QueueSnapshotAsync(It.IsAny<List<QueueItem>>()),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeQueueAsync_AssignsCorrectPositions()
    {
        // Arrange
        var tasks = new[]
        {
            CreateMockQueuedTask("https://example.com/file1.zip", CoreModels.DownloadPriority.High),
            CreateMockQueuedTask("https://example.com/file2.zip", CoreModels.DownloadPriority.Normal),
            CreateMockQueuedTask("https://example.com/file3.zip", CoreModels.DownloadPriority.Low)
        };
        
        _mockQueueManager.Setup(q => q.GetQueuedTasks()).Returns(tasks);

        // Act
        await _hub.SubscribeQueueAsync();

        // Assert
        _mockCaller.Verify(
            c => c.QueueSnapshotAsync(It.Is<List<QueueItem>>(list =>
                list.Count == 3 &&
                list[0].Position == 1 &&
                list[1].Position == 2 &&
                list[2].Position == 3)),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeQueueAsync_MapsTaskPropertiesCorrectly()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var task = new TestFakeDownloadTask
        {
            Id = taskId,
            Url = new Uri("https://example.com/file.zip"),
            FileName = "file.zip",
            State = CoreModels.DownloadState.Queued,
            Priority = CoreModels.DownloadPriority.High,
            CreatedAt = DateTime.UtcNow,
            Options = new CoreModels.DownloadOptions
            {
                DestinationDirectory = "/tmp",
                FileName = "file.zip",
                Category = "general",
                MaxConnections = 4
            },
            Progress = new CoreModels.DownloadProgress { TaskId = taskId }
        };

        _mockQueueManager.Setup(q => q.GetQueuedTasks()).Returns([task]);

        // Act
        await _hub.SubscribeQueueAsync();

        // Assert
        _mockCaller.Verify(
            c => c.QueueSnapshotAsync(It.Is<List<QueueItem>>(list =>
                list.Count == 1 &&
                list[0].DownloadId == taskId &&
                list[0].Priority == DownloadPriority.High &&
                list[0].Position == 1)),
            Times.Once);
    }

    [Fact]
    public async Task MultipleClients_CanSubscribeConcurrently()
    {
        // Arrange
        _mockQueueManager.Setup(q => q.GetQueuedTasks()).Returns([]);

        // Act
        await Task.WhenAll(
            _hub.SubscribeQueueAsync(),
            _hub.SubscribeQueueAsync(),
            _hub.SubscribeQueueAsync()
        );

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", QueueHub.GroupName, default),
            Times.Exactly(3));
    }

    private static IDownloadTask CreateMockQueuedTask(
        string url,
        CoreModels.DownloadPriority priority)
    {
        var uri = new Uri(url);
        var fileName = Path.GetFileName(uri.LocalPath);
        
        return new TestFakeDownloadTask
        {
            Id = Guid.NewGuid(),
            Url = uri,
            FileName = fileName,
            State = CoreModels.DownloadState.Queued,
            Priority = priority,
            CreatedAt = DateTime.UtcNow,
            Options = new CoreModels.DownloadOptions
            {
                DestinationDirectory = "/tmp",
                Category = "general",
                MaxConnections = 4
            },
            Progress = new CoreModels.DownloadProgress { TaskId = Guid.NewGuid() }
        };
    }
}

file sealed class TestFakeDownloadTask : IDownloadTask
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
}
