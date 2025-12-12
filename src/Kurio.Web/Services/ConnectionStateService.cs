using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Web.Services;

public enum ConnectionStatusKind
{
    Unknown,
    Connecting,
    Connected,
    Reconnecting,
    Disconnected,
    Error
}

public sealed record ConnectionStatusSnapshot(ConnectionStatusKind Status, DateTimeOffset UpdatedAtUtc, string? Message = null);

/// <summary>
/// Background health monitor that pings the API and exposes the latest connection snapshot.
/// </summary>
public sealed class ConnectionStateService : BackgroundService
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);

    private readonly KurioApiClient _apiClient;
    private readonly ILogger<ConnectionStateService> _logger;
    private readonly PeriodicTimer _timer = new(ProbeInterval);

    private int _isProbing;
    private ConnectionStatusSnapshot _snapshot = new(ConnectionStatusKind.Unknown, DateTimeOffset.UtcNow, "Not started");

    public ConnectionStateService(KurioApiClient apiClient, ILogger<ConnectionStateService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public ConnectionStatusSnapshot Current => _snapshot;

    public event Action<ConnectionStatusSnapshot>? Changed;

    public Task TriggerProbeAsync(CancellationToken cancellationToken = default) => ProbeAsync(true, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProbeAsync(immediate: true, stoppingToken).ConfigureAwait(false);

        while (await _timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await ProbeAsync(immediate: false, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProbeAsync(bool immediate, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _isProbing, 1) == 1)
        {
            return;
        }

        try
        {
            if (immediate)
            {
                UpdateSnapshot(new(ConnectionStatusKind.Connecting, DateTimeOffset.UtcNow));
            }

            var healthy = await _apiClient.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
            if (healthy)
            {
                UpdateSnapshot(new(ConnectionStatusKind.Connected, DateTimeOffset.UtcNow));
                return;
            }

            UpdateSnapshot(new(ConnectionStatusKind.Error, DateTimeOffset.UtcNow, "Health check failed"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogConnectionProbeFailed(ex);
            UpdateSnapshot(new(ConnectionStatusKind.Error, DateTimeOffset.UtcNow, ex.Message));
        }
        finally
        {
            Interlocked.Exchange(ref _isProbing, 0);
        }
    }

    private void UpdateSnapshot(ConnectionStatusSnapshot snapshot)
    {
        _snapshot = snapshot;
        Changed?.Invoke(snapshot);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Dispose();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal static partial class ConnectionStateLogging
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Connection probe failed")]
    public static partial void LogConnectionProbeFailed(this ILogger logger, Exception exception);
}
