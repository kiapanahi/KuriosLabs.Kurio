using FluentAssertions;
using KuriousLabs.Kurio.Contracts.Downloads;
using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using CoreModels = KuriousLabs.Kurio.Core.Models;

namespace Kurio.Server.Tests.Hubs;

public sealed class DownloadHubTests
{
    private readonly Mock<IDownloadEngine> _mockEngine;
    private readonly Mock<ILogger<DownloadHub>> _mockLogger;
    private readonly Mock<IHubCallerClients<IDownloadsClient>> _mockClients;
    private readonly Mock<IDownloadsClient> _mockCaller;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly DownloadHub _hub;

    public DownloadHubTests()
    {
        _mockEngine = new Mock<IDownloadEngine>();
        _mockLogger = new Mock<ILogger<DownloadHub>>();
        _mockClients = new Mock<IHubCallerClients<IDownloadsClient>>();
        _mockCaller = new Mock<IDownloadsClient>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _mockClients.Setup(c => c.Caller).Returns(_mockCaller.Object);
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");

        _hub = new DownloadHub(_mockEngine.Object, _mockLogger.Object)
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    [Fact]
    public async Task SubscribeDownloadsAsync_AddsClientToGroup()
    {
        // Arrange
        var request = new DownloadSubscriptionRequest { States = DownloadStateFilter.All };
        _mockEngine.Setup(e => e.GetDownloads(It.IsAny<CoreModels.DownloadStateFilter>()))
            .Returns([]);

        // Act
        await _hub.SubscribeDownloadsAsync(request);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", DownloadHub.GroupName, default),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeDownloadsAsync_SendsSnapshot()
    {
        // Arrange
        var request = new DownloadSubscriptionRequest { States = DownloadStateFilter.Active };
        var mockTask = CreateMockDownloadTask("https://example.com/file.zip", CoreModels.DownloadState.Downloading);
        
        _mockEngine.Setup(e => e.GetDownloads(It.IsAny<CoreModels.DownloadStateFilter>()))
            .Returns([mockTask]);

        // Act
        await _hub.SubscribeDownloadsAsync(request);

        // Assert
        _mockCaller.Verify(
            c => c.ReceiveSnapshotAsync(It.Is<List<DownloadSummary>>(list => list.Count == 1)),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeDownloadsAsync_FiltersCompletedDownloads_WhenIncludeCompletedIsFalse()
    {
        // Arrange
        var request = new DownloadSubscriptionRequest 
        { 
            States = DownloadStateFilter.All,
            IncludeCompleted = false
        };
        
        var activeTask = CreateMockDownloadTask("https://example.com/active.zip", CoreModels.DownloadState.Downloading);
        var completedTask = CreateMockDownloadTask("https://example.com/completed.zip", CoreModels.DownloadState.Completed);
        
        _mockEngine.Setup(e => e.GetDownloads(It.IsAny<CoreModels.DownloadStateFilter>()))
            .Returns([activeTask, completedTask]);

        // Act
        await _hub.SubscribeDownloadsAsync(request);

        // Assert
        _mockCaller.Verify(
            c => c.ReceiveSnapshotAsync(It.Is<List<DownloadSummary>>(
                list => list.Count == 1 && list[0].State == DownloadState.Downloading)),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeDownloadsAsync_AppliesLimit_WhenLimitSpecified()
    {
        // Arrange
        var request = new DownloadSubscriptionRequest 
        { 
            States = DownloadStateFilter.All,
            Limit = 2
        };
        
        var tasks = new[]
        {
            CreateMockDownloadTask("https://example.com/file1.zip", CoreModels.DownloadState.Downloading),
            CreateMockDownloadTask("https://example.com/file2.zip", CoreModels.DownloadState.Downloading),
            CreateMockDownloadTask("https://example.com/file3.zip", CoreModels.DownloadState.Downloading)
        };
        
        _mockEngine.Setup(e => e.GetDownloads(It.IsAny<CoreModels.DownloadStateFilter>()))
            .Returns(tasks);

        // Act
        await _hub.SubscribeDownloadsAsync(request);

        // Assert
        _mockCaller.Verify(
            c => c.ReceiveSnapshotAsync(It.Is<List<DownloadSummary>>(list => list.Count == 2)),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeDownloadsAsync_FiltersByCategory_WhenCategorySpecified()
    {
        // Arrange
        var request = new DownloadSubscriptionRequest 
        { 
            States = DownloadStateFilter.All,
            Category = "videos"
        };
        
        var videoTask = CreateMockDownloadTask("https://example.com/video.mp4", CoreModels.DownloadState.Downloading, "videos");
        var docTask = CreateMockDownloadTask("https://example.com/doc.pdf", CoreModels.DownloadState.Downloading, "documents");
        
        _mockEngine.Setup(e => e.GetDownloads(It.IsAny<CoreModels.DownloadStateFilter>()))
            .Returns([videoTask, docTask]);

        // Act
        await _hub.SubscribeDownloadsAsync(request);

        // Assert
        _mockCaller.Verify(
            c => c.ReceiveSnapshotAsync(It.Is<List<DownloadSummary>>(
                list => list.Count == 1 && list[0].Name == "video.mp4")),
            Times.Once);
    }

    [Fact]
    public async Task UnsubscribeDownloadsAsync_RemovesClientFromGroup()
    {
        // Act
        await _hub.UnsubscribeDownloadsAsync();

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync("test-connection-id", DownloadHub.GroupName, default),
            Times.Once);
    }

    [Fact]
    public async Task RequestSnapshotAsync_SendsSnapshot()
    {
        // Arrange
        _mockEngine.Setup(e => e.GetDownloads(It.IsAny<CoreModels.DownloadStateFilter>()))
            .Returns([]);

        // Act
        await _hub.RequestSnapshotAsync();

        // Assert
        _mockCaller.Verify(
            c => c.ReceiveSnapshotAsync(It.IsAny<List<DownloadSummary>>()),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeDownloadsAsync_OrdersByPriorityThenCreatedAt()
    {
        // Arrange
        var request = new DownloadSubscriptionRequest { States = DownloadStateFilter.All };
        
        var lowPriorityOld = CreateMockDownloadTask(
            "https://example.com/low-old.zip", 
            CoreModels.DownloadState.Downloading,
            priority: CoreModels.DownloadPriority.Low,
            createdAt: DateTime.UtcNow.AddHours(-2));
            
        var highPriorityNew = CreateMockDownloadTask(
            "https://example.com/high-new.zip", 
            CoreModels.DownloadState.Downloading,
            priority: CoreModels.DownloadPriority.High,
            createdAt: DateTime.UtcNow);
            
        var normalPriority = CreateMockDownloadTask(
            "https://example.com/normal.zip", 
            CoreModels.DownloadState.Downloading,
            priority: CoreModels.DownloadPriority.Normal,
            createdAt: DateTime.UtcNow.AddHours(-1));
        
        _mockEngine.Setup(e => e.GetDownloads(It.IsAny<CoreModels.DownloadStateFilter>()))
            .Returns([lowPriorityOld, normalPriority, highPriorityNew]);

        // Act
        await _hub.SubscribeDownloadsAsync(request);

        // Assert
        _mockCaller.Verify(
            c => c.ReceiveSnapshotAsync(It.Is<List<DownloadSummary>>(list =>
                list.Count == 3 &&
                list[0].Name == "high-new.zip" &&
                list[1].Name == "normal.zip" &&
                list[2].Name == "low-old.zip")),
            Times.Once);
    }

    private static IDownloadTask CreateMockDownloadTask(
        string url,
        CoreModels.DownloadState state,
        string? category = null,
        CoreModels.DownloadPriority priority = CoreModels.DownloadPriority.Normal,
        DateTime? createdAt = null)
    {
        var uri = new Uri(url);
        var fileName = Path.GetFileName(uri.LocalPath);
        
        return new TestFakeDownloadTask
        {
            Id = Guid.NewGuid(),
            Url = uri,
            FileName = fileName,
            State = state,
            Priority = priority,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            Options = new CoreModels.DownloadOptions
            {
                DestinationDirectory = "/tmp",
                Category = category,
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
