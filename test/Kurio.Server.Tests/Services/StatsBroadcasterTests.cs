using FluentAssertions;
using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Contracts.Stats;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Hubs;
using KuriousLabs.Kurio.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using CoreModels = KuriousLabs.Kurio.Core.Models;

namespace Kurio.Server.Tests.Services;

public sealed class StatsBroadcasterTests
{
    private sealed class FastStatsBroadcaster(
        IStatisticsService statisticsService,
        IDownloadQueueManager queueManager,
        IHubContext<StatsHub, IStatsClient> hubContext,
        ILogger<StatsBroadcaster> logger)
        : StatsBroadcaster(statisticsService, queueManager, hubContext, logger)
    {
        protected override TimeSpan BroadcastInterval => TimeSpan.FromMilliseconds(20);
    }

    [Fact]
    public async Task Broadcasting_ContinuesAfterTransientFailure()
    {
        // Arrange: first stats fetch throws, every later one succeeds.
        var stats = new CoreModels.DownloadStatistics { SessionStartedAt = DateTime.UtcNow };
        var calls = 0;

        var mockStatisticsService = new Mock<IStatisticsService>();
        mockStatisticsService
            .Setup(s => s.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .Returns(() => Interlocked.Increment(ref calls) == 1
                ? Task.FromException<CoreModels.DownloadStatistics>(new InvalidOperationException("transient failure"))
                : Task.FromResult(stats));

        var mockQueueManager = new Mock<IDownloadQueueManager>();
        var mockStatsClient = new Mock<IStatsClient>();
        var mockClients = new Mock<IHubClients<IStatsClient>>();
        mockClients.Setup(c => c.Group(StatsHub.GroupName)).Returns(mockStatsClient.Object);
        var mockHubContext = new Mock<IHubContext<StatsHub, IStatsClient>>();
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        using var broadcaster = new FastStatsBroadcaster(
            mockStatisticsService.Object,
            mockQueueManager.Object,
            mockHubContext.Object,
            Mock.Of<ILogger<StatsBroadcaster>>());

        // Act: run long enough for at least one tick after the failing one.
        await broadcaster.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (Volatile.Read(ref calls) < 2 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }
        finally
        {
            await broadcaster.StopAsync(CancellationToken.None);
        }

        // Assert: the loop survived the failed tick and kept broadcasting.
        calls.Should().BeGreaterThanOrEqualTo(2);
        mockStatsClient.Verify(c => c.StatsUpdatedAsync(It.IsAny<StatsSnapshot>()), Times.AtLeastOnce);
    }
}
