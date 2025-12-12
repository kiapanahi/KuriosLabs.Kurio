using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Hubs;
using KuriousLabs.Kurio.Server.Mappers;

using Microsoft.AspNetCore.SignalR;

namespace KuriousLabs.Kurio.Server.Services;

public class StatsBroadcaster : BackgroundService
{
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(5);

    private readonly IStatisticsService _statisticsService;
    private readonly IDownloadQueueManager _queueManager;
    private readonly IHubContext<StatsHub, IStatsClient> _hubContext;
    private readonly ILogger<StatsBroadcaster> _logger;

    public StatsBroadcaster(
        IStatisticsService statisticsService,
        IDownloadQueueManager queueManager,
        IHubContext<StatsHub, IStatsClient> hubContext,
        ILogger<StatsBroadcaster> logger)
    {
        _statisticsService = statisticsService;
        _queueManager = queueManager;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stats broadcaster started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await BroadcastAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(BroadcastInterval, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stats broadcaster stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stats broadcaster encountered an error");
        }
    }

    private async Task BroadcastAsync(CancellationToken cancellationToken)
    {
        var stats = await _statisticsService.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = stats.ToContract(_queueManager);

        await _hubContext.Clients.Group(StatsHub.GroupName)
            .StatsUpdatedAsync(snapshot)
            .ConfigureAwait(false);
    }
}
