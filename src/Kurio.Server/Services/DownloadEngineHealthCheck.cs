using Kurio.Core.Abstractions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KuriousLabs.Kurio.Server.Services;

/// <summary>
///     Health check for the download engine.
/// </summary>
public class DownloadEngineHealthCheck : IHealthCheck
{
    private readonly IDownloadEngine _engine;

    public DownloadEngineHealthCheck(IDownloadEngine engine)
    {
        _engine = engine;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if engine is responsive by getting queue statistics
            var (active, queued) = _engine.GetQueueStatistics();

            Dictionary<string, object> data = new() { { "activeDownloads", active }, { "queuedDownloads", queued } };

            return Task.FromResult(
                HealthCheckResult.Healthy("Download engine is operational", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Download engine is not responding", ex));
        }
    }
}
