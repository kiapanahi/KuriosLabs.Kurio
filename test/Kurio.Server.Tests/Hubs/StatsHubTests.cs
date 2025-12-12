using FluentAssertions;
using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Contracts.Stats;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using CoreModels = KuriousLabs.Kurio.Core.Models;

namespace Kurio.Server.Tests.Hubs;

public sealed class StatsHubTests
{
    private readonly Mock<IStatisticsService> _mockStatisticsService;
    private readonly Mock<IDownloadQueueManager> _mockQueueManager;
    private readonly Mock<ILogger<StatsHub>> _mockLogger;
    private readonly Mock<IHubCallerClients<IStatsClient>> _mockClients;
    private readonly Mock<IStatsClient> _mockCaller;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly StatsHub _hub;

    public StatsHubTests()
    {
        _mockStatisticsService = new Mock<IStatisticsService>();
        _mockQueueManager = new Mock<IDownloadQueueManager>();
        _mockLogger = new Mock<ILogger<StatsHub>>();
        _mockClients = new Mock<IHubCallerClients<IStatsClient>>();
        _mockCaller = new Mock<IStatsClient>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _mockClients.Setup(c => c.Caller).Returns(_mockCaller.Object);
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
        _mockContext.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);

        _hub = new StatsHub(
            _mockStatisticsService.Object,
            _mockQueueManager.Object,
            _mockLogger.Object)
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    [Fact]
    public async Task SubscribeStatsAsync_AddsClientToGroup()
    {
        // Arrange
        var stats = CreateMockStatistics();
        _mockStatisticsService.Setup(s => s.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
        _mockQueueManager.Setup(q => q.GetActiveCount()).Returns(0);
        _mockQueueManager.Setup(q => q.GetQueuedCount()).Returns(0);

        // Act
        await _hub.SubscribeStatsAsync();

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", StatsHub.GroupName, default),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeStatsAsync_SendsStatsSnapshot()
    {
        // Arrange
        var stats = CreateMockStatistics(
            completedDownloads: 10,
            failedDownloads: 2,
            totalBytesDownloaded: 1024000,
            averageSpeed: 512000);
            
        _mockStatisticsService.Setup(s => s.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
        _mockQueueManager.Setup(q => q.GetActiveCount()).Returns(3);
        _mockQueueManager.Setup(q => q.GetQueuedCount()).Returns(5);

        // Act
        await _hub.SubscribeStatsAsync();

        // Assert
        _mockCaller.Verify(
            c => c.StatsSnapshotAsync(It.Is<StatsSnapshot>(snapshot =>
                snapshot.CompletedCount == 10 &&
                snapshot.FailedCount == 2 &&
                snapshot.TotalBytesDownloaded == 1024000 &&
                snapshot.ActiveCount == 3 &&
                snapshot.QueuedCount == 5)),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeStatsAsync_MapsAllStatisticsFields()
    {
        // Arrange
        var startedAt = DateTime.UtcNow.AddHours(-2);
        var stats = new CoreModels.DownloadStatistics
        {
            AllTimeCompletedDownloads = 50,
            AllTimeFailedDownloads = 5,
            AllTimeBytesDownloaded = 10_000_000,
            AverageDownloadSpeed = 1_000_000,
            SessionStartedAt = startedAt
        };
        
        _mockStatisticsService.Setup(s => s.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
        _mockQueueManager.Setup(q => q.GetActiveCount()).Returns(2);
        _mockQueueManager.Setup(q => q.GetQueuedCount()).Returns(8);

        // Act
        await _hub.SubscribeStatsAsync();

        // Assert
        _mockCaller.Verify(
            c => c.StatsSnapshotAsync(It.Is<StatsSnapshot>(snapshot =>
                snapshot.CompletedCount == 50 &&
                snapshot.FailedCount == 5 &&
                snapshot.TotalBytesDownloaded == 10_000_000 &&
                snapshot.AverageThroughputBytesPerSecond == 1_000_000 &&
                snapshot.CurrentThroughputBytesPerSecond == 1_000_000 &&
                snapshot.ActiveCount == 2 &&
                snapshot.QueuedCount == 8 &&
                snapshot.StartedAt == new DateTimeOffset(startedAt))),
            Times.Once);
    }

    [Fact]
    public async Task UnsubscribeStatsAsync_RemovesClientFromGroup()
    {
        // Act
        await _hub.UnsubscribeStatsAsync();

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync("test-connection-id", StatsHub.GroupName, default),
            Times.Once);
    }

    [Fact]
    public async Task RequestStatsSnapshotAsync_SendsSnapshot()
    {
        // Arrange
        var stats = CreateMockStatistics();
        _mockStatisticsService.Setup(s => s.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
        _mockQueueManager.Setup(q => q.GetActiveCount()).Returns(0);
        _mockQueueManager.Setup(q => q.GetQueuedCount()).Returns(0);

        // Act
        await _hub.RequestStatsSnapshotAsync();

        // Assert
        _mockCaller.Verify(
            c => c.StatsSnapshotAsync(It.IsAny<StatsSnapshot>()),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeStatsAsync_HandlesZeroStatistics()
    {
        // Arrange
        var stats = CreateMockStatistics(0, 0, 0, 0);
        _mockStatisticsService.Setup(s => s.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
        _mockQueueManager.Setup(q => q.GetActiveCount()).Returns(0);
        _mockQueueManager.Setup(q => q.GetQueuedCount()).Returns(0);

        // Act
        await _hub.SubscribeStatsAsync();

        // Assert
        _mockCaller.Verify(
            c => c.StatsSnapshotAsync(It.Is<StatsSnapshot>(snapshot =>
                snapshot.CompletedCount == 0 &&
                snapshot.FailedCount == 0 &&
                snapshot.TotalBytesDownloaded == 0 &&
                snapshot.AverageThroughputBytesPerSecond == 0 &&
                snapshot.ActiveCount == 0 &&
                snapshot.QueuedCount == 0)),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeStatsAsync_UsesConnectionAbortedToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockContext.Setup(c => c.ConnectionAborted).Returns(cts.Token);
        
        var stats = CreateMockStatistics();
        _mockStatisticsService.Setup(s => s.GetStatisticsAsync(cts.Token))
            .ReturnsAsync(stats);
        _mockQueueManager.Setup(q => q.GetActiveCount()).Returns(0);
        _mockQueueManager.Setup(q => q.GetQueuedCount()).Returns(0);

        // Act
        await _hub.SubscribeStatsAsync();

        // Assert
        _mockStatisticsService.Verify(
            s => s.GetStatisticsAsync(cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeStatsAsync_RetrievesQueueCountsFromQueueManager()
    {
        // Arrange
        var stats = CreateMockStatistics();
        _mockStatisticsService.Setup(s => s.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
        _mockQueueManager.Setup(q => q.GetActiveCount()).Returns(7);
        _mockQueueManager.Setup(q => q.GetQueuedCount()).Returns(13);

        // Act
        await _hub.SubscribeStatsAsync();

        // Assert
        _mockQueueManager.Verify(q => q.GetActiveCount(), Times.AtLeastOnce);
        _mockQueueManager.Verify(q => q.GetQueuedCount(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task MultipleClients_CanSubscribeConcurrently()
    {
        // Arrange
        var stats = CreateMockStatistics();
        _mockStatisticsService.Setup(s => s.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
        _mockQueueManager.Setup(q => q.GetActiveCount()).Returns(0);
        _mockQueueManager.Setup(q => q.GetQueuedCount()).Returns(0);

        // Act
        await Task.WhenAll(
            _hub.SubscribeStatsAsync(),
            _hub.SubscribeStatsAsync(),
            _hub.SubscribeStatsAsync()
        );

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", StatsHub.GroupName, default),
            Times.Exactly(3));
    }

    private static CoreModels.DownloadStatistics CreateMockStatistics(
        int completedDownloads = 0,
        int failedDownloads = 0,
        long totalBytesDownloaded = 0,
        double averageSpeed = 0)
    {
        return new CoreModels.DownloadStatistics
        {
            AllTimeCompletedDownloads = completedDownloads,
            AllTimeFailedDownloads = failedDownloads,
            AllTimeBytesDownloaded = totalBytesDownloaded,
            AverageDownloadSpeed = averageSpeed,
            SessionStartedAt = DateTime.UtcNow
        };
    }
}
