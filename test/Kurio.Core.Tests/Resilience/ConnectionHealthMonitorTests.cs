using Kurio.Core.Abstractions;
using Kurio.Core.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Kurio.Core.Tests.Resilience;

public sealed class ConnectionHealthMonitorTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<ConnectionHealthMonitor>> _loggerMock;
    private readonly ConnectionResilienceOptions _options;
    private ConnectionHealthMonitor? _monitor;

    public ConnectionHealthMonitorTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<ConnectionHealthMonitor>>();
        _options = new ConnectionResilienceOptions
        {
            EnableConnectionMonitoring = true,
            NetworkHealthCheckIntervalSeconds = 1,
            HealthCheckTimeoutSeconds = 5,
            ConsecutiveFailuresThreshold = 2,
            HealthCheckEndpoints = ["https://httpbin.org/status/200"]
        };
    }

    public void Dispose()
    {
        _monitor?.Dispose();
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act
        _monitor = new ConnectionHealthMonitor(
            _httpClientFactoryMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        // Assert
        Assert.NotNull(_monitor);
        Assert.True(_monitor.IsHealthy); // Should start as healthy
    }

    [Fact]
    public async Task CheckHealthAsync_WithSuccessfulResponse_ReturnsTrue()
    {
        // Arrange
        var httpClient = new HttpClient();
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("KurioHealthCheck"))
            .Returns(httpClient);

        _monitor = new ConnectionHealthMonitor(
            _httpClientFactoryMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        // Act
        var result = await _monitor.CheckHealthAsync();

        // Assert
        Assert.True(result);
        Assert.True(_monitor.IsHealthy);
        Assert.NotNull(_monitor.LastHealthyAt);
    }

    [Fact]
    public async Task CheckHealthAsync_WithFailedResponse_ReturnsFalse()
    {
        // Arrange
        _options.HealthCheckEndpoints = ["https://invalid-domain-that-does-not-exist-12345.com"];
        _options.HealthCheckTimeoutSeconds = 1;
        _options.ConsecutiveFailuresThreshold = 1;

        var httpClient = new HttpClient();
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("KurioHealthCheck"))
            .Returns(httpClient);

        _monitor = new ConnectionHealthMonitor(
            _httpClientFactoryMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        // Act
        var result = await _monitor.CheckHealthAsync();

        // Assert
        Assert.False(result);
        Assert.False(_monitor.IsHealthy);
    }

    [Fact]
    public async Task HealthChanged_Event_RaisedOnStatusChange()
    {
        // Arrange
        _options.HealthCheckEndpoints = ["https://invalid-domain-12345.com"];
        _options.HealthCheckTimeoutSeconds = 1;
        _options.ConsecutiveFailuresThreshold = 1;

        var httpClient = new HttpClient();
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("KurioHealthCheck"))
            .Returns(httpClient);

        _monitor = new ConnectionHealthMonitor(
            _httpClientFactoryMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        var eventRaised = false;
        _monitor.HealthChanged += (sender, args) =>
        {
            eventRaised = true;
            Assert.False(args.IsHealthy);
        };

        // Act
        await _monitor.CheckHealthAsync();

        // Assert
        Assert.True(eventRaised);
    }

    [Fact]
    public async Task StartMonitoringAsync_StartsBackgroundMonitoring()
    {
        // Arrange
        var httpClient = new HttpClient();
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("KurioHealthCheck"))
            .Returns(httpClient);

        _monitor = new ConnectionHealthMonitor(
            _httpClientFactoryMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        // Act
        await _monitor.StartMonitoringAsync();
        await Task.Delay(2000); // Wait for at least one check

        // Assert
        Assert.True(_monitor.IsHealthy);
    }

    [Fact]
    public async Task WaitForHealthyConnectionAsync_ReturnsTrue_WhenHealthy()
    {
        // Arrange
        var httpClient = new HttpClient();
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("KurioHealthCheck"))
            .Returns(httpClient);

        _monitor = new ConnectionHealthMonitor(
            _httpClientFactoryMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        // Act
        var result = await _monitor.WaitForHealthyConnectionAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ConsecutiveFailures_IncrementCorrectly()
    {
        // Arrange
        _options.HealthCheckEndpoints = ["https://invalid-domain-12345.com"];
        _options.HealthCheckTimeoutSeconds = 1;
        _options.ConsecutiveFailuresThreshold = 3;

        var httpClient = new HttpClient();
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("KurioHealthCheck"))
            .Returns(httpClient);

        _monitor = new ConnectionHealthMonitor(
            _httpClientFactoryMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        // Act
        await _monitor.CheckHealthAsync(); // First failure
        Assert.Equal(1, _monitor.ConsecutiveFailures);
        Assert.True(_monitor.IsHealthy); // Still healthy (threshold is 3)

        await _monitor.CheckHealthAsync(); // Second failure
        Assert.Equal(2, _monitor.ConsecutiveFailures);
        Assert.True(_monitor.IsHealthy); // Still healthy

        await _monitor.CheckHealthAsync(); // Third failure
        Assert.Equal(3, _monitor.ConsecutiveFailures);
        Assert.False(_monitor.IsHealthy); // Now unhealthy
    }
}

