# Connection Resilience and Network Failure Recovery

This feature provides robust handling of unreliable internet connections, including automatic detection of network failures, intelligent retry mechanisms, and seamless recovery without data loss.

## Features

- **Automatic Network Failure Detection**: Detects connection drops, timeouts, and stalled transfers
- **Intelligent Retry with Exponential Backoff**: Automatically retries failed segments with increasing delays
- **Connection Health Monitoring**: Periodic health checks to known endpoints
- **Stall Detection**: Detects when transfers stop receiving data for extended periods
- **Seamless Recovery**: Automatically resumes downloads when connection is restored
- **Data Integrity**: Ensures no data corruption during network interruptions
- **Configurable Behavior**: Fine-tune retry strategies and health check parameters

## Architecture

### Components

1. **IConnectionHealthMonitor**: Monitors internet connectivity in the background
2. **ConnectionResilienceOptions**: Configuration for retry strategies and health checks
3. **ResiliencePolicyFactory**: Creates Polly-based resilience pipelines
4. **Enhanced SegmentManager**: Applies resilience policies to segment downloads
5. **Enhanced HttpProtocolHandler**: Detects stalled transfers

### How It Works

```
┌─────────────────┐
│ Download Engine │
└────────┬────────┘
         │
         ├──> ┌────────────────┐      ┌──────────────────────┐
         │    │ Segment Manager│─────>│ Resilience Pipeline  │
         │    └────────────────┘      │ - Retry              │
         │                             │ - Circuit Breaker    │
         │                             │ - Timeout            │
         │                             └──────────────────────┘
         │
         └──> ┌────────────────────────┐
              │ Connection Health      │
              │ Monitor (Background)   │
              │ - Periodic checks      │
              │ - Status tracking      │
              └────────────────────────┘
```

## Configuration

### appsettings.json

```json
{
  "ConnectionResilience": {
    "MaxRetryAttempts": 5,
    "InitialRetryDelaySeconds": 2,
    "MaxRetryDelaySeconds": 60,
    "NetworkHealthCheckIntervalSeconds": 30,
    "StallDetectionTimeoutSeconds": 30,
    "EnableConnectionMonitoring": true,
    "EnableAdaptiveBackoff": true,
    "EnableCircuitBreaker": true,
    "HealthCheckEndpoints": [
      "https://www.google.com",
      "https://www.cloudflare.com",
      "https://www.microsoft.com"
    ],
    "HealthCheckTimeoutSeconds": 5,
    "UseJitter": true,
    "ConsecutiveFailuresThreshold": 3,
    "AutoPauseOnConnectionLoss": false,
    "AutoResumeOnConnectionRecovery": true
  }
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `MaxRetryAttempts` | 5 | Maximum number of retry attempts for failed segments |
| `InitialRetryDelaySeconds` | 2 | Initial delay before first retry |
| `MaxRetryDelaySeconds` | 60 | Maximum delay between retries |
| `NetworkHealthCheckIntervalSeconds` | 30 | How often to check connection health |
| `StallDetectionTimeoutSeconds` | 30 | Timeout for detecting stalled transfers |
| `EnableConnectionMonitoring` | true | Enable background connection monitoring |
| `EnableAdaptiveBackoff` | true | Use exponential backoff for retries |
| `EnableCircuitBreaker` | true | Enable circuit breaker pattern |
| `HealthCheckEndpoints` | [...] | Endpoints to check for connectivity |
| `HealthCheckTimeoutSeconds` | 5 | Timeout for health check requests |
| `UseJitter` | true | Add random jitter to retry delays |
| `ConsecutiveFailuresThreshold` | 3 | Failures before marking connection unhealthy |
| `AutoPauseOnConnectionLoss` | false | Automatically pause downloads on connection loss |
| `AutoResumeOnConnectionRecovery` | true | Automatically resume on recovery |

## Usage

### Programmatic Usage

```csharp
// The connection resilience is automatically integrated
// when you use AddKurioDownloadEngine()

services.AddKurioDownloadEngine(tempDir, stateDir);

// Optionally customize options
services.Configure<ConnectionResilienceOptions>(options =>
{
    options.MaxRetryAttempts = 10;
    options.InitialRetryDelaySeconds = 1;
    options.EnableConnectionMonitoring = true;
});
```

### Monitoring Connection Health

```csharp
// Inject the connection health monitor
public class MyService
{
    private readonly IConnectionHealthMonitor _healthMonitor;

    public MyService(IConnectionHealthMonitor healthMonitor)
    {
        _healthMonitor = healthMonitor;
        
        // Subscribe to health changes
        _healthMonitor.HealthChanged += OnHealthChanged;
    }

    private void OnHealthChanged(object? sender, ConnectionHealthChangedEventArgs e)
    {
        if (e.IsHealthy)
        {
            Console.WriteLine("Connection restored!");
        }
        else
        {
            Console.WriteLine($"Connection lost after {e.ConsecutiveFailures} failures");
        }
    }

    public async Task StartAsync()
    {
        // Start monitoring
        await _healthMonitor.StartMonitoringAsync();
    }
}
```

### Waiting for Connection Recovery

```csharp
// Wait up to 60 seconds for connection to recover
var isHealthy = await _healthMonitor.WaitForHealthyConnectionAsync(
    TimeSpan.FromSeconds(60));

if (isHealthy)
{
    // Connection is back, resume operations
    await _downloadEngine.ResumeDownloadAsync(taskId);
}
```

## Retry Strategy

The retry strategy uses exponential backoff with jitter:

```
Attempt 1: Wait 2s
Attempt 2: Wait 4s (+ random jitter)
Attempt 3: Wait 8s (+ random jitter)
Attempt 4: Wait 16s (+ random jitter)
Attempt 5: Wait 32s (+ random jitter)
Maximum wait: 60s
```

Jitter helps prevent thundering herd problems when many segments retry simultaneously.

## Error Classification

### Transient Errors (Automatically Retried)

- Connection refused
- Connection reset
- Connection aborted
- Network down/unreachable
- Socket timeout
- Server errors (5xx)

### Permanent Errors (Not Retried)

- 401 Unauthorized
- 403 Forbidden
- 404 Not Found
- 410 Gone

## Stall Detection

Downloads are monitored for activity. If no data is received for `StallDetectionTimeoutSeconds` (default: 30s), the transfer is considered stalled and automatically retried.

```csharp
// In HttpProtocolHandler
const int stallTimeoutSeconds = 30;
while ((bytesRead = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
{
    // Check for stall
    var timeSinceLastData = DateTime.UtcNow - lastDataReceivedAt;
    if (timeSinceLastData.TotalSeconds > stallTimeoutSeconds)
    {
        throw new TimeoutException("Download stalled");
    }
}
```

## Testing

### Unit Tests

```bash
# Run connection resilience tests
dotnet test --filter "FullyQualifiedName~Resilience"
```

### Integration Tests

```bash
# Simulate network failure
# On macOS/Linux:
sudo ifconfig en0 down   # Disable network
# Wait a few seconds
sudo ifconfig en0 up     # Re-enable network

# On Windows:
netsh interface set interface "Wi-Fi" disable
netsh interface set interface "Wi-Fi" enable
```

### Network Simulation

You can use tools like `toxiproxy` or `comcast` to simulate unreliable networks:

```bash
# Install toxiproxy
brew install toxiproxy

# Simulate latency
toxiproxy-cli create -l localhost:8474 -u httpbin.org:80 httpbin
toxiproxy-cli toxic add -t latency -a latency=1000 httpbin
```

## Performance Considerations

- **Connection Pooling**: HTTP connections are pooled and reused
- **Concurrent Retries**: Each segment retries independently
- **Circuit Breaker**: Prevents cascading failures
- **Health Checks**: Lightweight HEAD requests to minimize overhead
- **Configurable Intervals**: Adjust health check frequency based on needs

## Troubleshooting

### Downloads Keep Failing

1. Check network stability with health monitor
2. Increase `MaxRetryAttempts` in configuration
3. Adjust `StallDetectionTimeoutSeconds` for slow connections
4. Review logs for error patterns

### Too Many Retry Attempts

1. Reduce `MaxRetryAttempts`
2. Increase `InitialRetryDelaySeconds`
3. Enable circuit breaker to fail fast
4. Check if server is blocking requests

### Health Checks Failing

1. Verify health check endpoints are accessible
2. Check firewall/proxy settings
3. Adjust `HealthCheckTimeoutSeconds`
4. Add alternative health check endpoints

## Logging

Connection resilience components emit structured logs:

```
[Info] Starting connection health monitoring (interval: 30s)
[Warning] Network retry 2/5 after 4s - Error type: ConnectionReset
[Warning] Connection lost after 3 consecutive failures
[Info] Connection restored
[Warning] Download stalled: no data received for 30s on range 0-1024
```

Configure logging levels in appsettings.json:

```json
{
  "Logging": {
    "LogLevel": {
      "Kurio.Core.Resilience": "Information",
      "Kurio.Core.Engine.SegmentManager": "Information"
    }
  }
}
```

## Future Enhancements

- [ ] Machine learning for adaptive retry strategies
- [ ] Multi-source downloading (P2P-style)
- [ ] Bandwidth estimation and adaptation
- [ ] Predictive pre-emptive pausing
- [ ] Integration with OS network state APIs
- [ ] Per-download resilience configuration

## Related Documentation

- [PRD: Connection Resilience](../prd/connection-resilience.md)
- [Architecture Overview](../architecture/domain-model.md)
- [Configuration Guide](../../README.md#configuration)

## Version History

- **v1.11.0** (2025-11-28): Initial connection resilience implementation
  - Connection health monitoring
  - Network-aware retry policies
  - Stall detection
  - Configurable resilience options

