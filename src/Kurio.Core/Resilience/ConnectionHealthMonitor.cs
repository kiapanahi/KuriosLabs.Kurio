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
    private readonly SemaphoreSlim _stateLock = new(1, 1);

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
        _stateLock.Dispose();
    }

    /// <inheritdoc />
    public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableConnectionMonitoring)
        {
            _logger.LogInformation("Connection monitoring is disabled");
            return Task.CompletedTask;
        }

        if (_monitoringTask != null)
        {
            _logger.LogWarning("Connection monitoring is already running");
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Starting connection health monitoring (interval: {Interval}s)",
            _options.NetworkHealthCheckIntervalSeconds);

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

        _logger.LogInformation("Stopping connection health monitoring");

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

                _logger.LogDebug("Checking connection health via {Endpoint}", endpoint);

                using var request = new HttpRequestMessage(HttpMethod.Head, endpoint);
                using var response = await httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Health check succeeded via {Endpoint}", endpoint);
                    await UpdateHealthStatusAsync(true, null);
                    return true;
                }

                _logger.LogDebug(
                    "Health check failed via {Endpoint}: {StatusCode}",
                    endpoint,
                    response.StatusCode);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogDebug(ex, "Health check failed via {Endpoint}", endpoint);
                // Try next endpoint
            }
        }

        // All endpoints failed
        await UpdateHealthStatusAsync(false, "All health check endpoints failed");
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
            if (await CheckHealthAsync(cancellationToken))
            {
                return true;
            }

            // Wait before next check
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds),
                    cancellationToken);
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
                await CheckHealthAsync(cancellationToken);

                await Task.Delay(
                    TimeSpan.FromSeconds(_options.NetworkHealthCheckIntervalSeconds),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Monitoring stopped
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection health monitoring");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        _logger.LogInformation("Connection health monitoring stopped");
    }

    /// <summary>
    ///     Updates the health status and raises events if changed.
    /// </summary>
    private async Task UpdateHealthStatusAsync(bool isHealthy, string? error)
    {
        await _stateLock.WaitAsync();
        try
        {
            var previousHealth = _isHealthy;

            if (isHealthy)
            {
                _consecutiveFailures = 0;
                _lastHealthyAt = DateTime.UtcNow;

                if (!_isHealthy)
                {
                    _isHealthy = true;
                    _logger.LogInformation("Connection restored");
                    OnHealthChanged(true, null);
                }
            }
            else
            {
                _consecutiveFailures++;

                if (_consecutiveFailures >= _options.ConsecutiveFailuresThreshold && _isHealthy)
                {
                    _isHealthy = false;
                    _logger.LogWarning(
                        "Connection lost after {Count} consecutive failures",
                        _consecutiveFailures);
                    OnHealthChanged(false, error);
                }
            }
        }
        finally
        {
            _stateLock.Release();
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

