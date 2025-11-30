using KuriousLabs.Kurio.Core.Abstractions;

namespace KuriousLabs.Kurio.Server.Services;

/// <summary>
///     Background service that hosts the download engine and manages its lifecycle.
/// </summary>
public class DownloadEngineHostedService : IHostedService
{
    private readonly IDownloadEngine _engine;
    private readonly ILogger<DownloadEngineHostedService> _logger;

    public DownloadEngineHostedService(
        IDownloadEngine engine,
        ILogger<DownloadEngineHostedService> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Download engine hosted service starting");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Download engine hosted service stopping");

        try
        {
            // Pause all active downloads gracefully
            var pausedCount = await _engine.PauseAllAsync(cancellationToken);
            _logger.LogInformation("Paused {Count} downloads before shutdown", pausedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while pausing downloads during shutdown");
        }
    }
}
