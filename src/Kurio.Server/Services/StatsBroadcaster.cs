using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Hubs;
using KuriousLabs.Kurio.Server.Mappers;

using Microsoft.AspNetCore.SignalR;

namespace KuriousLabs.Kurio.Server.Services;

public class StatsBroadcaster : BackgroundService
{
    // Virtual so tests can shrink the interval without reflection or InternalsVisibleTo.
    protected virtual TimeSpan BroadcastInterval => TimeSpan.FromSeconds(5);

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

        using PeriodicTimer timer = new(BroadcastInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BroadcastAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed tick must not kill the broadcaster for the host's lifetime.
                _logger.LogError(ex, "Stats broadcast tick failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Stats broadcaster stopped");
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
