using System.Collections.Concurrent;

using KuriousLabs.Kurio.Core.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KuriousLabs.Kurio.Core.Resilience;

/// <summary>
///     Monitors internet connection health by periodically checking connectivity to known endpoints.
/// </summary>
public sealed class ConnectionHealthMonitor : IConnectionHealthMonitor
{
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ConnectionHealthMonitor> _logger;
    private readonly ConnectionResilienceOptions _options;
    private readonly Lock _stateLock = new();

    private int _consecutiveFailures;
    private bool _disposed;
    private bool _isHealthy = true;
    private DateTime? _lastHealthyAt;
    private Task? _monitoringTask;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConnectionHealthMonitor" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="options">The connection resilience options.</param>
    /// <param name="logger">The logger instance.</param>
    public ConnectionHealthMonitor(
        IHttpClientFactory httpClientFactory,
        IOptions<ConnectionResilienceOptions> options,
        ILogger<ConnectionHealthMonitor> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsHealthy => _isHealthy;

    /// <inheritdoc />
    public DateTime? LastHealthyAt => _lastHealthyAt;

    /// <inheritdoc />
    public int ConsecutiveFailures => _consecutiveFailures;

    /// <inheritdoc />
    public event EventHandler<ConnectionHealthChangedEventArgs>? HealthChanged;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _disposalCts.Cancel();
        _disposalCts.Dispose();
    }

    /// <inheritdoc />
    public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableConnectionMonitoring)
        {
            _logger.LogMonitoringDisabled();
            return Task.CompletedTask;
        }

        if (_monitoringTask != null)
        {
            _logger.LogMonitoringAlreadyRunning();
            return Task.CompletedTask;
        }

        _logger.LogMonitoringStarting(_options.NetworkHealthCheckIntervalSeconds);

        _monitoringTask = Task.Run(() => MonitorConnectionHealthAsync(_disposalCts.Token), cancellationToken);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopMonitoringAsync()
    {
        if (_monitoringTask == null)
        {
            return Task.CompletedTask;
        }

        _logger.LogMonitoringStopping();

        _disposalCts.Cancel();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        // Try each health check endpoint until one succeeds
        foreach (var endpoint in _options.HealthCheckEndpoints)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("KurioHealthCheck");

                _logger.LogCheckingHealthViaEndpoint(endpoint);

                using var request = new HttpRequestMessage(HttpMethod.Head, endpoint);
                using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogHealthCheckSucceededViaEndpoint(endpoint);
                    UpdateHealthStatus(true, null);
                    return true;
                }

                _logger.LogHealthCheckFailedViaEndpointWithStatus(endpoint, response.StatusCode);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogHealthCheckFailedViaEndpoint(ex, endpoint);
                // Try next endpoint
            }
        }

        // All endpoints failed
        UpdateHealthStatus(false, "All health check endpoints failed");
        return false;
    }

    /// <inheritdoc />
    public async Task<bool> WaitForHealthyConnectionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (_isHealthy)
            {
                return true;
            }

            // Check health immediately
            if (await CheckHealthAsync(cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            // Wait before next check
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Background task that monitors connection health periodically.
    /// </summary>
    private async Task MonitorConnectionHealthAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CheckHealthAsync(cancellationToken).ConfigureAwait(false);

                await Task.Delay(
                    TimeSpan.FromSeconds(_options.NetworkHealthCheckIntervalSeconds),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Monitoring stopped
                break;
            }
            catch (Exception ex)
            {
                _logger.LogMonitoringError(ex);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogMonitoringStopped();
    }

    /// <summary>
    ///     Updates the health status and raises events if changed.
    /// </summary>
    private void UpdateHealthStatus(bool isHealthy, string? error)
    {
        lock (_stateLock)
        {
            var previousHealth = _isHealthy;

            if (isHealthy)
            {
                _consecutiveFailures = 0;
                _lastHealthyAt = DateTime.UtcNow;

                if (!_isHealthy)
                {
                    _isHealthy = true;
                    _logger.LogConnectionRestored();
                    OnHealthChanged(true, null);
                }
            }
            else
            {
                _consecutiveFailures++;

                if (_consecutiveFailures >= _options.ConsecutiveFailuresThreshold && _isHealthy)
                {
                    _isHealthy = false;
                    _logger.LogConnectionLost(_consecutiveFailures);
                    OnHealthChanged(false, error);
                }
            }
        }
    }

    /// <summary>
    ///     Raises the HealthChanged event.
    /// </summary>
    private void OnHealthChanged(bool isHealthy, string? error)
    {
        HealthChanged?.Invoke(this, new ConnectionHealthChangedEventArgs
        {
            IsHealthy = isHealthy,
            Timestamp = DateTime.UtcNow,
            ConsecutiveFailures = _consecutiveFailures,
            LastError = error
        });
    }
}

