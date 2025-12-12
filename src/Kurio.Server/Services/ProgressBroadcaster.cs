using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Hubs;
using KuriousLabs.Kurio.Server.Mappers;

using Microsoft.AspNetCore.SignalR;

namespace KuriousLabs.Kurio.Server.Services;

/// <summary>
///     Background service that broadcasts progress updates to all connected SignalR clients.
/// </summary>
public class ProgressBroadcaster : BackgroundService
{
    private readonly IDownloadEngine _engine;
    private readonly IHubContext<DownloadHub, IDownloadsClient> _hubContext;
    private readonly ILogger<ProgressBroadcaster> _logger;

    public ProgressBroadcaster(
        IDownloadEngine engine,
        IHubContext<DownloadHub, IDownloadsClient> hubContext,
        ILogger<ProgressBroadcaster> logger)
    {
        _engine = engine;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Progress broadcaster started");

        try
        {
            await foreach (var progress in _engine.StreamProgressAsync(null, stoppingToken))
            {
                try
                {
                    var update = progress.ToProgressUpdate();
                    await _hubContext.Clients.Group(DownloadHub.GroupName)
                        .DownloadProgressedAsync(update)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting progress for task {TaskId}", progress.TaskId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Progress broadcaster stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Progress broadcaster encountered an error");
        }
    }
}
