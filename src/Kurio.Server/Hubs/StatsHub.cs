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

    public async Task SubscribeStatsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Client {ConnectionId} subscribed to stats", Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName, cancellationToken).ConfigureAwait(false);
        await SendSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UnsubscribeStatsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Client {ConnectionId} unsubscribed from stats", Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName, cancellationToken).ConfigureAwait(false);
    }

    public async Task RequestStatsSnapshotAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Client {ConnectionId} requested stats snapshot", Context.ConnectionId);
        await SendSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SendSnapshotAsync(CancellationToken cancellationToken)
    {
        var stats = await _statisticsService.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = stats.ToContract(_queueManager);
        await Clients.Caller.StatsSnapshotAsync(snapshot).ConfigureAwait(false);
    }
}
