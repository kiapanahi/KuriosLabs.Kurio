using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using KuriousLabs.Kurio.Contracts.Stats;
using KuriousLabs.Kurio.Core.Abstractions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using CoreModels = KuriousLabs.Kurio.Core.Models;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Kurio.Server.Tests;

public class StatsEndpointsTests
{
    private const string StatsUrl = "/api/stats";

    [Fact]
    public async Task StatsSnapshot_MapsStatistics()
    {
        using var factory = new ServerTestFactory();
        var client = factory.WithStats(stats =>
        {
            stats.AllTimeBytesDownloaded = 10_000;
            stats.AllTimeCompletedDownloads = 5;
            stats.AllTimeFailedDownloads = 2;
            stats.AverageDownloadSpeed = 1234;
            stats.SessionStartedAt = new DateTime(2024, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        }).WithQueue(queue => queue.SetCounts(active: 1, queued: 2)).CreateClient();

        var snapshot = await client.GetFromJsonAsync<StatsSnapshot>(StatsUrl);

        snapshot.Should().NotBeNull();
        snapshot!.ActiveCount.Should().Be(1);
        snapshot.QueuedCount.Should().Be(2);
        snapshot.CompletedCount.Should().Be(5);
        snapshot.FailedCount.Should().Be(2);
        snapshot.TotalBytesDownloaded.Should().Be(10_000);
        snapshot.AverageThroughputBytesPerSecond.Should().Be(1234);
        snapshot.CurrentThroughputBytesPerSecond.Should().Be(1234);
        snapshot.StartedAt.Should().Be(new DateTimeOffset(new DateTime(2024, 12, 1, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task Stats_WithNoActivity_ReturnsZeroedSnapshot()
    {
        using var factory = new ServerTestFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(StatsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = await response.Content.ReadFromJsonAsync<StatsSnapshot>();

        snapshot.Should().NotBeNull();
        snapshot!.ActiveCount.Should().Be(0);
        snapshot.QueuedCount.Should().Be(0);
        snapshot.CompletedCount.Should().Be(0);
        snapshot.FailedCount.Should().Be(0);
        snapshot.TotalBytesDownloaded.Should().Be(0);
        snapshot.AverageThroughputBytesPerSecond.Should().Be(0);
        snapshot.CurrentThroughputBytesPerSecond.Should().Be(0);

        // DownloadStatistics.SessionStartedAt defaults to DateTime.UtcNow, and the mapper always
        // projects it, so StartedAt is populated even for an untouched engine.
        snapshot.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Stats_ReturnsJsonContentType()
    {
        using var factory = new ServerTestFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(StatsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Content.Headers.ContentType.CharSet.Should().Be("utf-8");
    }

    /// <summary>
    ///     Asserts the raw wire payload, not just the deserialized object: the property casing is
    ///     exactly what the MVC pipeline produced, and no extra or renamed members appeared.
    /// </summary>
    [Fact]
    public async Task Stats_RawJson_UsesCamelCasePropertyNames()
    {
        using var factory = new ServerTestFactory();
        var client = factory.WithStats(stats =>
        {
            stats.AllTimeBytesDownloaded = 7;
            stats.AllTimeCompletedDownloads = 3;
            stats.AllTimeFailedDownloads = 1;
            stats.AverageDownloadSpeed = 42;
            stats.SessionStartedAt = new DateTime(2024, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        }).WithQueue(queue => queue.SetCounts(active: 2, queued: 4)).CreateClient();

        var json = await client.GetStringAsync(StatsUrl);

        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        properties.Should().BeEquivalentTo(
            [
                "activeCount",
                "queuedCount",
                "completedCount",
                "failedCount",
                "currentThroughputBytesPerSecond",
                "averageThroughputBytesPerSecond",
                "totalBytesDownloaded",
                "startedAt"
            ],
            options => options.WithStrictOrdering());

        // Guard against a serializer regression that would emit the CLR names verbatim.
        json.Should().NotContain("\"ActiveCount\"");
        json.Should().NotContain("\"StartedAt\"");

        document.RootElement.GetProperty("activeCount").GetInt32().Should().Be(2);
        document.RootElement.GetProperty("queuedCount").GetInt32().Should().Be(4);
        document.RootElement.GetProperty("completedCount").GetInt32().Should().Be(3);
        document.RootElement.GetProperty("failedCount").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("currentThroughputBytesPerSecond").GetInt64().Should().Be(42);
        document.RootElement.GetProperty("averageThroughputBytesPerSecond").GetInt64().Should().Be(42);
        document.RootElement.GetProperty("totalBytesDownloaded").GetInt64().Should().Be(7);
    }

    /// <summary>
    ///     The one <see cref="DateTimeOffset" /> on the wire must keep the exact ISO-8601 shape
    ///     System.Text.Json produced under MVC (offset suffix, no trailing fractional zeros).
    /// </summary>
    [Fact]
    public async Task Stats_RawJson_SerializesStartedAtAsIso8601WithOffset()
    {
        var startedAt = new DateTime(2024, 12, 1, 12, 34, 56, DateTimeKind.Utc);

        using var factory = new ServerTestFactory();
        var client = factory.WithStats(stats => stats.SessionStartedAt = startedAt).CreateClient();

        var json = await client.GetStringAsync(StatsUrl);

        using var document = JsonDocument.Parse(json);
        var raw = document.RootElement.GetProperty("startedAt");

        raw.ValueKind.Should().Be(JsonValueKind.String);
        raw.GetString().Should().Be("2024-12-01T12:34:56+00:00");
        raw.GetDateTimeOffset().Should().Be(new DateTimeOffset(startedAt));

        // And it round-trips back through the client deserializer unchanged.
        var snapshot = JsonSerializer.Deserialize<StatsSnapshot>(json, JsonSerializerOptions.Web);
        snapshot!.StartedAt.Should().Be(new DateTimeOffset(startedAt));
    }

    /// <summary>
    ///     <see cref="StatsSnapshot" /> has no enum member and no member that can serialize as
    ///     null (the mapper always projects <c>StartedAt</c> from a non-nullable
    ///     <see cref="DateTime" />), so the string-enum and null-omission settings are not
    ///     observable on this payload. Assert them on the options instance the endpoint's
    ///     <c>TypedResults.Ok</c> actually uses instead, and prove the behaviour through it.
    /// </summary>
    [Fact]
    public void Stats_UsesConfiguredHttpJsonOptions_StringEnumsAndNullOmission()
    {
        using var factory = new ServerTestFactory();
        _ = factory.CreateClient();

        var options = factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        options.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
        options.DefaultIgnoreCondition.Should().Be(JsonIgnoreCondition.WhenWritingNull);
        options.Converters.Should().ContainItemsAssignableTo<JsonStringEnumConverter>();

        var probe = JsonSerializer.Serialize(
            new SerializerProbe { Severity = AlertSeverity.Warning, Optional = null, Value = 1 },
            options);

        probe.Should().Be("{\"severity\":\"Warning\",\"value\":1}");
    }

    /// <summary>
    ///     MVC ran with the default <c>MvcOptions.ReturnHttpNotAcceptable = false</c>, so an
    ///     unsatisfiable <c>Accept</c> header still produced 200 + JSON rather than 406. Minimal
    ///     APIs do not negotiate at all, so this asserts the two agree.
    /// </summary>
    [Fact]
    public async Task Stats_WithUnsatisfiableAcceptHeader_StillReturnsJson()
    {
        using var factory = new ServerTestFactory();
        var client = factory.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Get, StatsUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Stats_RejectsNonGetVerbs()
    {
        using var factory = new ServerTestFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync(StatsUrl, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    /// <summary>
    ///     The request's <see cref="CancellationToken" /> must reach
    ///     <c>IStatisticsService.GetStatisticsAsync</c>: aborting the HTTP request has to cancel
    ///     the in-flight service call rather than leave it running.
    /// </summary>
    [Fact]
    public async Task Stats_HonoursRequestCancellation()
    {
        BlockingStatisticsService statistics = new();

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IStatisticsService>(statistics).CreateClient();

        using CancellationTokenSource requestCts = new();
        var requestTask = client.GetAsync(StatsUrl, requestCts.Token);

        await statistics.Entered.WaitAsync(TimeSpan.FromSeconds(10));

        statistics.ObservedToken.CanBeCanceled.Should().BeTrue("the endpoint must forward the request token, not CancellationToken.None");

        await requestCts.CancelAsync();

        await statistics.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(10));

        var act = async () => await requestTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    ///     Cross-cutting smoke test for the shared <c>Program.cs</c> after the
    ///     <c>AddControllers()</c>/<c>MapControllers()</c> removal: health checks still respond and
    ///     the OpenAPI document is still generated and still describes the stats path.
    /// </summary>
    [Fact]
    public async Task Health_AndOpenApiDocument_AreStillServed()
    {
        using var factory = new ServerTestFactory();
        using var developmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        var client = developmentFactory.CreateClient();

        var health = await client.GetAsync("/health");
        health.StatusCode.Should().Be(HttpStatusCode.OK);
        (await health.Content.ReadAsStringAsync()).Should().Be("Healthy");

        var openApi = await client.GetAsync("/openapi/v1.json");
        openApi.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await openApi.Content.ReadAsStringAsync());
        document.RootElement.TryGetProperty("paths", out var paths).Should().BeTrue();
        paths.TryGetProperty(StatsUrl, out var statsPath).Should().BeTrue("the stats endpoint must still appear in the OpenAPI document");
        statsPath.TryGetProperty("get", out _).Should().BeTrue();
    }

    private sealed record SerializerProbe
    {
        public AlertSeverity Severity { get; init; }

        public string? Optional { get; init; }

        public int Value { get; init; }
    }

    /// <summary>
    ///     Statistics service that blocks until its <see cref="CancellationToken" /> is cancelled,
    ///     so a test can observe whether the request token is actually plumbed through.
    /// </summary>
    private sealed class BlockingStatisticsService : IStatisticsService
    {
        private readonly TaskCompletionSource _cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public Task CancellationObserved => _cancellationObserved.Task;

        public CancellationToken ObservedToken { get; private set; }

        public async Task<CoreModels.DownloadStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            ObservedToken = cancellationToken;
            _entered.TrySetResult();

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _cancellationObserved.TrySetResult();
                throw;
            }

            return new CoreModels.DownloadStatistics();
        }

        public Task RecordCompletedDownloadAsync(CoreModels.DownloadHistoryEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordFailedDownloadAsync(CoreModels.DownloadHistoryEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ResetSessionStatisticsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IDictionary<string, object>> ExportStatisticsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>());
    }
}
