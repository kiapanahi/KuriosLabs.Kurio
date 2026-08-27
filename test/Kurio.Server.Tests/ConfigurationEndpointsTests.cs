using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Configuration;
using KuriousLabs.Kurio.Server.Models;

namespace Kurio.Server.Tests;

/// <summary>
///     Wire-level tests for the <c>/api/config</c> minimal-API endpoints.
/// </summary>
public class ConfigurationEndpointsTests
{
    [Fact]
    public async Task GetSpeedLimit_ReturnsConfiguredValuesAndLiveLimiterSpeed()
    {
        var configService = new FakeConfigurationService();
        configService.Configuration.Network.BandwidthLimit.Enabled = true;
        configService.Configuration.Network.BandwidthLimit.MaxDownloadSpeed = 4096;
        configService.Configuration.Network.BandwidthLimit.MaxUploadSpeed = 2048;
        var speedLimiter = new FakeSpeedLimiter { MaxBytesPerSecond = 1234 };

        using var factory = new ServerTestFactory();
        var client = factory
            .WithService<IConfigurationService>(configService)
            .WithService<ISpeedLimiter>(speedLimiter)
            .CreateClient();

        var response = await client.GetAsync("/api/config/speed-limit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadFromJsonAsync<SpeedLimitResponse>();
        body.Should().NotBeNull();
        body!.Enabled.Should().BeTrue();
        body.MaxDownloadSpeedBytesPerSecond.Should().Be(4096);
        body.MaxUploadSpeedBytesPerSecond.Should().Be(2048);

        // The current limit comes from the live limiter, not from the stored configuration.
        body.CurrentLimitBytesPerSecond.Should().Be(1234);
    }

    [Fact]
    public async Task GetSpeedLimit_UsesCamelCasePropertyNames()
    {
        var configService = new FakeConfigurationService();
        var speedLimiter = new FakeSpeedLimiter();

        using var factory = new ServerTestFactory();
        var client = factory
            .WithService<IConfigurationService>(configService)
            .WithService<ISpeedLimiter>(speedLimiter)
            .CreateClient();

        var json = await client.GetStringAsync("/api/config/speed-limit");

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .Should()
            .BeEquivalentTo(
                "enabled",
                "maxDownloadSpeedBytesPerSecond",
                "maxUploadSpeedBytesPerSecond",
                "currentLimitBytesPerSecond");
    }

    [Fact]
    public async Task UpdateSpeedLimit_PersistsConfigurationAndAppliesLimiterSpeed()
    {
        var configService = new FakeConfigurationService();
        var speedLimiter = new FakeSpeedLimiter();

        using var factory = new ServerTestFactory();
        var client = factory
            .WithService<IConfigurationService>(configService)
            .WithService<ISpeedLimiter>(speedLimiter)
            .CreateClient();

        UpdateSpeedLimitRequest request = new()
        {
            Enabled = true,
            MaxDownloadSpeedBytesPerSecond = 1_048_576,
            MaxUploadSpeedBytesPerSecond = 524_288
        };

        var response = await client.PutAsJsonAsync("/api/config/speed-limit", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SpeedLimitResponse>();
        body.Should().NotBeNull();
        body!.Enabled.Should().BeTrue();
        body.MaxDownloadSpeedBytesPerSecond.Should().Be(1_048_576);
        body.MaxUploadSpeedBytesPerSecond.Should().Be(524_288);
        body.CurrentLimitBytesPerSecond.Should().Be(1_048_576);

        configService.UpdateCallCount.Should().Be(1);
        configService.Configuration.Network.BandwidthLimit.Enabled.Should().BeTrue();
        configService.Configuration.Network.BandwidthLimit.MaxDownloadSpeed.Should().Be(1_048_576);
        configService.Configuration.Network.BandwidthLimit.MaxUploadSpeed.Should().Be(524_288);

        speedLimiter.UpdatedSpeeds.Should().Equal(1_048_576);

        // A follow-up read reflects the update.
        var reread = await client.GetFromJsonAsync<SpeedLimitResponse>("/api/config/speed-limit");
        reread.Should().NotBeNull();
        reread!.MaxDownloadSpeedBytesPerSecond.Should().Be(1_048_576);
    }

    [Fact]
    public async Task UpdateSpeedLimit_WhenDisabled_ReportsZeroCurrentLimit()
    {
        var configService = new FakeConfigurationService();
        var speedLimiter = new FakeSpeedLimiter { MaxBytesPerSecond = 9999 };

        using var factory = new ServerTestFactory();
        var client = factory
            .WithService<IConfigurationService>(configService)
            .WithService<ISpeedLimiter>(speedLimiter)
            .CreateClient();

        UpdateSpeedLimitRequest request = new()
        {
            Enabled = false,
            MaxDownloadSpeedBytesPerSecond = 5000,
            MaxUploadSpeedBytesPerSecond = 2500
        };

        var response = await client.PutAsJsonAsync("/api/config/speed-limit", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SpeedLimitResponse>();
        body.Should().NotBeNull();
        body!.Enabled.Should().BeFalse();

        // The configured maximum is still echoed back, but the effective limit is unlimited (0).
        body.MaxDownloadSpeedBytesPerSecond.Should().Be(5000);
        body.CurrentLimitBytesPerSecond.Should().Be(0);

        speedLimiter.UpdatedSpeeds.Should().Equal(0);
    }

    [Theory]
    [InlineData(-1, 0, "Download speed limit cannot be negative")]
    [InlineData(0, -1, "Upload speed limit cannot be negative")]
    public async Task UpdateSpeedLimit_WithNegativeSpeed_ReturnsBadRequestProblem(
        long downloadSpeed,
        long uploadSpeed,
        string expectedDetail)
    {
        var configService = new FakeConfigurationService();
        var speedLimiter = new FakeSpeedLimiter();

        using var factory = new ServerTestFactory();
        var client = factory
            .WithService<IConfigurationService>(configService)
            .WithService<ISpeedLimiter>(speedLimiter)
            .CreateClient();

        UpdateSpeedLimitRequest request = new()
        {
            Enabled = true,
            MaxDownloadSpeedBytesPerSecond = downloadSpeed,
            MaxUploadSpeedBytesPerSecond = uploadSpeed
        };

        var response = await client.PutAsJsonAsync("/api/config/speed-limit", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("title").GetString().Should().Be("Invalid speed limit");
        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("detail").GetString().Should().Be(expectedDetail);
        root.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();

        // Nulls are omitted, so the unset "instance" member must not appear on the wire.
        root.TryGetProperty("instance", out _).Should().BeFalse();

        // Nothing was persisted and the limiter was left alone.
        configService.UpdateCallCount.Should().Be(0);
        speedLimiter.UpdatedSpeeds.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateSpeedLimit_WhenConfigurationServiceThrows_ReturnsGenericServerError()
    {
        const string SecretDetail = "connection string password=hunter2";

        var configService = new FakeConfigurationService
        {
            UpdateException = new InvalidOperationException(SecretDetail)
        };
        var speedLimiter = new FakeSpeedLimiter();

        using var factory = new ServerTestFactory();
        var client = factory
            .WithService<IConfigurationService>(configService)
            .WithService<ISpeedLimiter>(speedLimiter)
            .CreateClient();

        UpdateSpeedLimitRequest request = new()
        {
            Enabled = true,
            MaxDownloadSpeedBytesPerSecond = 1000,
            MaxUploadSpeedBytesPerSecond = 1000
        };

        var response = await client.PutAsJsonAsync("/api/config/speed-limit", request);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("title").GetString().Should().Be("Failed to update speed limit");
        root.GetProperty("status").GetInt32().Should().Be(500);
        root.GetProperty("detail").GetString()
            .Should().Be("An unexpected error occurred while updating the speed limit.");
        root.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();

        // The exception must never reach the client (regression guard for commit e5c9086).
        json.Should().NotContain(SecretDetail);
        json.Should().NotContainEquivalentOf(nameof(InvalidOperationException));
        json.Should().NotContainEquivalentOf("stackTrace");
    }

    [Fact]
    public async Task GetConfiguration_ReturnsFullConfigurationInCamelCaseWithNullsOmitted()
    {
        var configService = new FakeConfigurationService();
        configService.Configuration.Version = "9.9";
        configService.Configuration.Downloads.MaxConcurrentDownloads = 7;
        configService.Configuration.Storage.Categorization.CustomRules.Add(new CategoryRule
        {
            Name = "archives",
            Category = "Archives",
            Extensions = [".zip"]
            // MimeTypes and UrlPattern are deliberately left null.
        });

        using var factory = new ServerTestFactory();
        var client = factory
            .WithService<IConfigurationService>(configService)
            .WithService<ISpeedLimiter>(new FakeSpeedLimiter())
            .CreateClient();

        var response = await client.GetAsync("/api/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var config = await response.Content.ReadFromJsonAsync<KurioConfiguration>();
        config.Should().NotBeNull();
        config!.Version.Should().Be("9.9");
        config.Downloads.MaxConcurrentDownloads.Should().Be(7);

        var json = await client.GetStringAsync("/api/config");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.TryGetProperty("version", out _).Should().BeTrue();
        root.TryGetProperty("Version", out _).Should().BeFalse();
        root.GetProperty("downloads").GetProperty("maxConcurrentDownloads").GetInt32().Should().Be(7);

        var rule = root.GetProperty("storage")
            .GetProperty("categorization")
            .GetProperty("customRules")[0];
        rule.GetProperty("extensions")[0].GetString().Should().Be(".zip");
        rule.TryGetProperty("mimeTypes", out _).Should().BeFalse();
        rule.TryGetProperty("urlPattern", out _).Should().BeFalse();
    }
}

/// <summary>
///     In-memory <see cref="IConfigurationService" /> that records update calls and can be told
///     to fail, so the endpoint's error path can be exercised.
/// </summary>
internal sealed class FakeConfigurationService : IConfigurationService
{
    /// <summary>The live configuration instance handed to callers.</summary>
    public KurioConfiguration Configuration { get; } = new();

    /// <summary>Number of times <see cref="UpdateConfigurationAsync" /> was invoked.</summary>
    public int UpdateCallCount { get; private set; }

    /// <summary>When set, <see cref="UpdateConfigurationAsync" /> throws this exception.</summary>
    public Exception? UpdateException { get; init; }

    public event EventHandler<KurioConfiguration>? ConfigurationChanged
    {
        add { }
        remove { }
    }

    public KurioConfiguration GetConfiguration()
    {
        return Configuration;
    }

    public Task UpdateConfigurationAsync(
        Action<KurioConfiguration> updateAction,
        CancellationToken cancellationToken = default)
    {
        if (UpdateException is not null)
        {
            throw UpdateException;
        }

        UpdateCallCount++;
        updateAction(Configuration);
        return Task.CompletedTask;
    }

    public ConfigurationValidationResult ValidateConfiguration(KurioConfiguration config)
    {
        return ConfigurationValidationResult.Success();
    }

    public Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ExportConfigurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ImportConfigurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
///     <see cref="ISpeedLimiter" /> that records every runtime speed change.
/// </summary>
internal sealed class FakeSpeedLimiter : ISpeedLimiter
{
    /// <summary>Every value passed to <see cref="UpdateMaxSpeed" />, in call order.</summary>
    public List<long> UpdatedSpeeds { get; } = [];

    public bool IsEnabled => MaxBytesPerSecond > 0;

    public long MaxBytesPerSecond { get; set; }

    public Task ThrottleAsync(int bytesRequested, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void UpdateMaxSpeed(long newMaxBytesPerSecond)
    {
        UpdatedSpeeds.Add(newMaxBytesPerSecond);
        MaxBytesPerSecond = newMaxBytesPerSecond;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
