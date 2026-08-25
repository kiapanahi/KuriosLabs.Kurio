using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Mappers;

using Microsoft.AspNetCore.SignalR;

namespace KuriousLabs.Kurio.Server.Hubs;

public sealed class StatsHub : Hub<IStatsClient>, IStatsHub
{
    public const string GroupName = "stats";

    private readonly IStatisticsService _statisticsService;
    private readonly IDownloadQueueManager _queueManager;
    private readonly ILogger<StatsHub> _logger;

    public StatsHub(
        IStatisticsService statisticsService,
        IDownloadQueueManager queueManager,
        ILogger<StatsHub> logger)
    {
        _statisticsService = statisticsService;
        _queueManager = queueManager;
        _logger = logger;
    }

    public async Task SubscribeStatsAsync()
    {
        _logger.LogClientSubscribedToStats(Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName).ConfigureAwait(false);
        await SendSnapshotAsync().ConfigureAwait(false);
    }

    public async Task UnsubscribeStatsAsync()
    {
        _logger.LogClientUnsubscribedFromStats(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName).ConfigureAwait(false);
    }

    public async Task RequestStatsSnapshotAsync()
    {
        _logger.LogClientRequestedStatsSnapshot(Context.ConnectionId);
        await SendSnapshotAsync().ConfigureAwait(false);
    }

    private async Task SendSnapshotAsync()
    {
        var stats = await _statisticsService.GetStatisticsAsync(Context.ConnectionAborted).ConfigureAwait(false);
        var snapshot = stats.ToContract(_queueManager);
        await Clients.Caller.StatsSnapshotAsync(snapshot).ConfigureAwait(false);
    }
}
