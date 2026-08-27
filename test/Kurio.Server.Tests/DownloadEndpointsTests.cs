using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Hubs;
using KuriousLabs.Kurio.Server.Models;

using Microsoft.AspNetCore.SignalR;

using Moq;

using ContractDownloads = KuriousLabs.Kurio.Contracts.Downloads;
using ContractQueue = KuriousLabs.Kurio.Contracts.Queue;
using CoreModels = KuriousLabs.Kurio.Core.Models;

namespace Kurio.Server.Tests;

/// <summary>
///     Wire-level tests for the minimal-API <c>/api/downloads</c> endpoints. They assert the
///     behaviour the former <c>DownloadsController</c> produced: routes, status codes, the
///     <c>ProblemDetails</c> payload shape and the JSON serialization settings.
/// </summary>
public class DownloadEndpointsTests
{
    /// <summary>
    ///     Mirrors the server's <c>ConfigureHttpJsonOptions</c> settings so the tests can read the
    ///     camelCase / string-enum payloads the API produces.
    /// </summary>
    private static readonly JsonSerializerOptions ClientJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // ---------------------------------------------------------------------------------------
    // GET /api/downloads
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task GetDownloads_WithoutFilterQueryString_Returns200AndDefaultsToNone()
    {
        // MVC bound a missing [FromQuery] enum to default(DownloadStateFilter) == None.
        // Minimal APIs 400 on a missing non-nullable value type unless the parameter has a default.
        var engine = new TestDownloadEngine();
        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.GetAsync("/api/downloads");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        engine.LastFilter.Should().Be(CoreModels.DownloadStateFilter.None);
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    [Fact]
    public async Task GetDownloads_WithFilterQueryString_PassesFilterToEngine()
    {
        var engine = new TestDownloadEngine();
        engine.Tasks.Add(CreateTask("active.bin", CoreModels.DownloadState.Downloading));
        engine.Tasks.Add(CreateTask("done.bin", CoreModels.DownloadState.Completed));

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.GetAsync("/api/downloads?filter=Active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        engine.LastFilter.Should().Be(CoreModels.DownloadStateFilter.Active);

        var downloads = await response.Content.ReadFromJsonAsync<List<DownloadResponse>>(ClientJson);
        downloads.Should().NotBeNull();
        downloads!.Should().ContainSingle();
        downloads[0].FileName.Should().Be("active.bin");
    }

    [Fact]
    public async Task GetDownloads_UsesCamelCaseStringEnumsAndOmitsNulls()
    {
        var engine = new TestDownloadEngine();
        engine.Tasks.Add(CreateTask(
            "file.bin",
            CoreModels.DownloadState.Queued,
            CoreModels.DownloadPriority.High,
            fileSize: 1024));

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.GetAsync("/api/downloads?filter=All");
        var json = await response.Content.ReadAsStringAsync();

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        // camelCase property names
        json.Should().Contain("\"fileName\":\"file.bin\"");
        json.Should().Contain("\"fileSize\":1024");
        json.Should().NotContain("\"FileName\"");

        // enums as strings
        json.Should().Contain("\"state\":\"Queued\"");
        json.Should().Contain("\"priority\":\"High\"");

        // nulls omitted (a queued task has no progress/started/completed/error)
        json.Should().NotContain("progress");
        json.Should().NotContain("startedAt");
        json.Should().NotContain("completedAt");
        json.Should().NotContain("errorMessage");
        json.Should().NotContain("null");
    }

    [Theory]
    [InlineData("Queued,Paused")]
    [InlineData("8")]
    [InlineData("All")]
    public async Task GetDownloads_AcceptsFlagListsAndNumericFilterValues(string filter)
    {
        var engine = new TestDownloadEngine();
        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.GetAsync($"/api/downloads?filter={Uri.EscapeDataString(filter)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("ALL")]
    [InlineData("qUeUeD,pAuSeD")]
    public async Task GetDownloads_ParsesFilterCaseInsensitively(string filter)
    {
        // MVC's EnumConverter parsed case-insensitively. Minimal APIs bind bare enums through
        // the case-SENSITIVE Enum.TryParse overload, so DownloadStateFilterQuery restores the
        // old, forgiving parse - without it these requests would break for existing clients.
        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(new TestDownloadEngine()).CreateClient();

        var response = await client.GetAsync($"/api/downloads?filter={Uri.EscapeDataString(filter)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    public async Task GetDownloads_WithUnparseableFilter_ReturnsBadRequest(string filter)
    {
        // MVC rejected these with a 400 in every environment. Minimal APIs default
        // RouteHandlerOptions.ThrowOnBadRequest to true in Development, which would surface a
        // bind failure as a 500 through UseExceptionHandler; Program.cs turns that off so the
        // status code stays 400 everywhere.
        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(new TestDownloadEngine()).CreateClient();

        var response = await client.GetAsync($"/api/downloads?filter={filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddDownload_WithMalformedJsonBody_ReturnsBadRequest()
    {
        // Same ThrowOnBadRequest concern as above, on the body-binding path. MVC's
        // [ApiController] answered a malformed body with a 400 ValidationProblemDetails; the
        // minimal-API body shape differs, but the status code must still be 400, not 500.
        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(new TestDownloadEngine()).CreateClient();

        using StringContent content = new("{ not json", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/downloads", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------------------------------
    // GET /api/downloads/{id}
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task GetDownload_WhenPresent_Returns200()
    {
        var task = CreateTask("one.bin", CoreModels.DownloadState.Paused);
        var engine = new TestDownloadEngine();
        engine.Tasks.Add(task);

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.GetAsync($"/api/downloads/{task.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DownloadResponse>(ClientJson);
        body.Should().NotBeNull();
        body!.Id.Should().Be(task.Id);
        body.FileName.Should().Be("one.bin");
        body.State.Should().Be(CoreModels.DownloadState.Paused);
    }

    [Fact]
    public async Task GetDownload_WhenMissing_Returns404ProblemDetails()
    {
        var id = Guid.NewGuid();
        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(new TestDownloadEngine()).CreateClient();

        var response = await client.GetAsync($"/api/downloads/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;
        root.GetProperty("title").GetString().Should().Be("Download not found");
        root.GetProperty("status").GetInt32().Should().Be(404);
        root.GetProperty("detail").GetString().Should().Be($"No download with ID {id} exists");
        root.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrWhiteSpace();
        root.TryGetProperty("type", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetDownload_WithNonGuidId_Returns404BecauseOfRouteConstraint()
    {
        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(new TestDownloadEngine()).CreateClient();

        var response = await client.GetAsync("/api/downloads/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------------------------
    // POST /api/downloads
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task AddDownload_Returns201WithLocationHeaderPointingAtGetDownload()
    {
        var created = CreateTask("new.bin", CoreModels.DownloadState.Queued);
        var engine = new TestDownloadEngine { AddedTask = created };

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/downloads",
            new AddDownloadRequest { Url = "https://example.com/new.bin" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        engine.LastAddedUrl.Should().Be(new Uri("https://example.com/new.bin"));

        // MVC's CreatedAtAction produced an absolute URL; CreatedAtRoute does the same.
        var location = response.Headers.Location;
        location.Should().NotBeNull();
        location!.IsAbsoluteUri.Should().BeTrue();
        location.AbsolutePath.Should().Be($"/api/downloads/{created.Id}");

        var body = await response.Content.ReadFromJsonAsync<DownloadResponse>(ClientJson);
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task AddDownload_WithNonNormalPriority_AppliesPriorityToNewTask()
    {
        var created = CreateTask("hot.bin", CoreModels.DownloadState.Queued);
        var engine = new TestDownloadEngine { AddedTask = created };

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/downloads",
            new AddDownloadRequest
            {
                Url = "https://example.com/hot.bin",
                Priority = CoreModels.DownloadPriority.High
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        engine.PriorityChanges.Should().ContainSingle()
            .Which.Should().Be((created.Id, CoreModels.DownloadPriority.High));
    }

    [Fact]
    public async Task AddDownload_WithNormalPriority_DoesNotCallChangePriority()
    {
        var engine = new TestDownloadEngine { AddedTask = CreateTask("plain.bin", CoreModels.DownloadState.Queued) };

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/downloads",
            new AddDownloadRequest { Url = "https://example.com/plain.bin" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        engine.PriorityChanges.Should().BeEmpty();
    }

    [Fact]
    public async Task AddDownload_WithInvalidUrl_Returns400InvalidUrlProblem()
    {
        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(new TestDownloadEngine()).CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/downloads",
            new AddDownloadRequest { Url = "not-a-valid-url" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("title").GetString().Should().Be("Invalid URL");
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        problem.RootElement.GetProperty("detail").GetString()
            .Should().Be("The URL 'not-a-valid-url' is not valid");
        problem.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AddDownload_WhenEngineThrows_Returns400FailedToAddDownload()
    {
        var engine = new TestDownloadEngine
        {
            AddDownloadException = new InvalidOperationException("engine is offline")
        };

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/downloads",
            new AddDownloadRequest { Url = "https://example.com/boom.bin" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("title").GetString().Should().Be("Failed to add download");
        problem.RootElement.GetProperty("detail").GetString().Should().Be("engine is offline");
    }

    // ---------------------------------------------------------------------------------------
    // POST /api/downloads/{id}/start|pause|resume
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("start")]
    [InlineData("pause")]
    [InlineData("resume")]
    public async Task LifecycleCommand_HappyPath_Returns204(string verb)
    {
        var task = CreateTask("life.bin", CoreModels.DownloadState.Queued);
        var engine = new TestDownloadEngine();
        engine.Tasks.Add(task);

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.PostAsync($"/api/downloads/{task.Id}/{verb}", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        engine.CommandCalls.Should().ContainSingle().Which.Should().Be((verb, task.Id));
    }

    [Theory]
    [InlineData("start", "Cannot start download")]
    [InlineData("pause", "Cannot pause download")]
    [InlineData("resume", "Cannot resume download")]
    public async Task LifecycleCommand_InvalidOperation_Returns400(string verb, string expectedTitle)
    {
        var id = Guid.NewGuid();
        var engine = new TestDownloadEngine();
        engine.CommandExceptions[verb] = new InvalidOperationException("wrong state");

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.PostAsync($"/api/downloads/{id}/{verb}", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("title").GetString().Should().Be(expectedTitle);
        problem.RootElement.GetProperty("detail").GetString().Should().Be("wrong state");
    }

    [Theory]
    [InlineData("start")]
    [InlineData("pause")]
    [InlineData("resume")]
    public async Task LifecycleCommand_KeyNotFound_Returns404(string verb)
    {
        var id = Guid.NewGuid();
        var engine = new TestDownloadEngine();
        engine.CommandExceptions[verb] = new KeyNotFoundException($"Download {id} was not found");

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.PostAsync($"/api/downloads/{id}/{verb}", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("title").GetString().Should().Be("Download not found");
        problem.RootElement.GetProperty("detail").GetString().Should().Be($"Download {id} was not found");
    }

    [Fact]
    public async Task StartDownload_BroadcastsUpdateAndQueueSnapshot()
    {
        var task = CreateTask("bcast.bin", CoreModels.DownloadState.Queued);
        var engine = new TestDownloadEngine();
        engine.Tasks.Add(task);

        var (downloadsHub, downloadsClient) = CreateDownloadsHub();
        var (queueHub, queueClient) = CreateQueueHub();

        using var factory = new ServerTestFactory();
        var client = factory
            .WithService<IDownloadEngine>(engine)
            .WithService(downloadsHub.Object)
            .WithService(queueHub.Object)
            .CreateClient();

        var response = await client.PostAsync($"/api/downloads/{task.Id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        downloadsClient.Verify(
            c => c.DownloadUpdatedAsync(It.Is<ContractDownloads.DownloadSummary>(s => s.Id == task.Id)),
            Times.Once);
        queueClient.Verify(
            c => c.QueueSnapshotAsync(It.IsAny<IReadOnlyList<ContractQueue.QueueItem>>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------------------------
    // DELETE /api/downloads/{id}
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task CancelDownload_WithoutRemoveFilesQueryString_Returns204AndDefaultsToFalse()
    {
        var id = Guid.NewGuid();
        var engine = new TestDownloadEngine();

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.DeleteAsync($"/api/downloads/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        engine.CancelCalls.Should().ContainSingle().Which.Should().Be((id, false));
    }

    [Fact]
    public async Task CancelDownload_WithRemoveFilesTrue_ForwardsFlag()
    {
        var id = Guid.NewGuid();
        var engine = new TestDownloadEngine();

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.DeleteAsync($"/api/downloads/{id}?removeFiles=true");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        engine.CancelCalls.Should().ContainSingle().Which.Should().Be((id, true));
    }

    [Fact]
    public async Task CancelDownload_BroadcastsRemoval()
    {
        var id = Guid.NewGuid();
        var (downloadsHub, downloadsClient) = CreateDownloadsHub();
        var (queueHub, queueClient) = CreateQueueHub();

        using var factory = new ServerTestFactory();
        var client = factory
            .WithService<IDownloadEngine>(new TestDownloadEngine())
            .WithService(downloadsHub.Object)
            .WithService(queueHub.Object)
            .CreateClient();

        var response = await client.DeleteAsync($"/api/downloads/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        downloadsClient.Verify(c => c.DownloadRemovedAsync(id), Times.Once);
        queueClient.Verify(
            c => c.QueueSnapshotAsync(It.IsAny<IReadOnlyList<ContractQueue.QueueItem>>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelDownload_InvalidOperation_Returns400()
    {
        var engine = new TestDownloadEngine { CancelException = new InvalidOperationException("already gone") };

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.DeleteAsync($"/api/downloads/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("title").GetString().Should().Be("Cannot cancel download");
        problem.RootElement.GetProperty("detail").GetString().Should().Be("already gone");
    }

    [Fact]
    public async Task CancelDownload_KeyNotFound_Returns404()
    {
        var engine = new TestDownloadEngine { CancelException = new KeyNotFoundException("nope") };

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.DeleteAsync($"/api/downloads/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("title").GetString().Should().Be("Download not found");
    }

    [Fact]
    public async Task CancelDownload_UnexpectedException_Returns500WithoutLeakingDetails()
    {
        var engine = new TestDownloadEngine
        {
            CancelException = new IOException("secret internal path /var/lib/kurio/state")
        };

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.DeleteAsync($"/api/downloads/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("secret internal path");

        using var problem = JsonDocument.Parse(raw);
        problem.RootElement.GetProperty("title").GetString().Should().Be("Failed to cancel download");
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(500);
        problem.RootElement.GetProperty("detail").GetString()
            .Should().Be("An unexpected error occurred while cancelling the download.");
    }

    // ---------------------------------------------------------------------------------------
    // POST /api/downloads/{id}/priority
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task ChangePriority_WhenEngineSucceeds_Returns204()
    {
        var id = Guid.NewGuid();
        var engine = new TestDownloadEngine();

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/downloads/{id}/priority",
            new ChangePriorityRequest { Priority = CoreModels.DownloadPriority.Low });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        engine.PriorityChanges.Should().ContainSingle()
            .Which.Should().Be((id, CoreModels.DownloadPriority.Low));
    }

    [Fact]
    public async Task ChangePriority_WhenEngineReturnsFalse_Returns404()
    {
        var id = Guid.NewGuid();
        var engine = new TestDownloadEngine { ChangePriorityResult = false };

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/downloads/{id}/priority",
            new ChangePriorityRequest { Priority = CoreModels.DownloadPriority.High });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("title").GetString()
            .Should().Be("Download not found or not queued");
        problem.RootElement.GetProperty("detail").GetString()
            .Should().Be($"Cannot change priority for download {id}");
    }

    [Fact]
    public async Task ChangePriority_AcceptsStringEnumInRequestBody()
    {
        var id = Guid.NewGuid();
        var engine = new TestDownloadEngine();

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        using StringContent content = new("{\"priority\":\"Critical\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/api/downloads/{id}/priority", content);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        engine.PriorityChanges.Should().ContainSingle()
            .Which.Priority.Should().Be(CoreModels.DownloadPriority.Critical);
    }

    // ---------------------------------------------------------------------------------------
    // POST /api/downloads/pause-all and /clear-completed, GET /api/downloads/statistics
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task PauseAll_Returns200WithIntBody()
    {
        var engine = new TestDownloadEngine { PauseAllResult = 3 };
        engine.Tasks.Add(CreateTask("a.bin", CoreModels.DownloadState.Downloading));

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.PostAsync("/api/downloads/pause-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync()).Should().Be("3");
        (await response.Content.ReadFromJsonAsync<int>()).Should().Be(3);
    }

    [Fact]
    public async Task ClearCompleted_Returns204AndBroadcastsClearedIds()
    {
        var completed = CreateTask("done.bin", CoreModels.DownloadState.Completed);
        var engine = new TestDownloadEngine();
        engine.Tasks.Add(completed);

        var (downloadsHub, downloadsClient) = CreateDownloadsHub();

        using var factory = new ServerTestFactory();
        var client = factory
            .WithService<IDownloadEngine>(engine)
            .WithService(downloadsHub.Object)
            .CreateClient();

        var response = await client.PostAsync("/api/downloads/clear-completed", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        engine.ClearCompletedCallCount.Should().Be(1);
        downloadsClient.Verify(
            c => c.DownloadsClearedAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(completed.Id))),
            Times.Once);
    }

    [Fact]
    public async Task ClearCompleted_WithNothingCompleted_Returns204AndDoesNotBroadcast()
    {
        var engine = new TestDownloadEngine();
        var (downloadsHub, downloadsClient) = CreateDownloadsHub();

        using var factory = new ServerTestFactory();
        var client = factory
            .WithService<IDownloadEngine>(engine)
            .WithService(downloadsHub.Object)
            .CreateClient();

        var response = await client.PostAsync("/api/downloads/clear-completed", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        engine.ClearCompletedCallCount.Should().Be(1);
        downloadsClient.Verify(
            c => c.DownloadsClearedAsync(It.IsAny<IReadOnlyCollection<Guid>>()),
            Times.Never);
    }

    [Fact]
    public async Task GetStatistics_Returns200WithQueueStatisticsShape()
    {
        var engine = new TestDownloadEngine { QueueStats = (2, 5) };
        engine.Tasks.Add(CreateTask("a.bin", CoreModels.DownloadState.Completed));
        engine.Tasks.Add(CreateTask("b.bin", CoreModels.DownloadState.Completed));
        engine.Tasks.Add(CreateTask("c.bin", CoreModels.DownloadState.Failed));
        engine.Tasks.Add(CreateTask("d.bin", CoreModels.DownloadState.Queued));

        using var factory = new ServerTestFactory();
        var client = factory.WithService<IDownloadEngine>(engine).CreateClient();

        var response = await client.GetAsync("/api/downloads/statistics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"activeDownloads\":2");
        json.Should().Contain("\"queuedDownloads\":5");

        var stats = await response.Content.ReadFromJsonAsync<QueueStatistics>(ClientJson);
        stats.Should().NotBeNull();
        stats!.ActiveDownloads.Should().Be(2);
        stats.QueuedDownloads.Should().Be(5);
        stats.TotalDownloads.Should().Be(4);
        stats.CompletedDownloads.Should().Be(2);
        stats.FailedDownloads.Should().Be(1);
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private static FakeDownloadTask CreateTask(
        string name,
        CoreModels.DownloadState state,
        CoreModels.DownloadPriority priority = CoreModels.DownloadPriority.Normal,
        long fileSize = 0)
    {
        var id = Guid.NewGuid();
        return new FakeDownloadTask
        {
            Id = id,
            Url = new Uri($"https://example.com/{name}"),
            FileName = name,
            FileSize = fileSize,
            State = state,
            Priority = priority,
            Progress = new CoreModels.DownloadProgress { TaskId = id },
            Options = new CoreModels.DownloadOptions
            {
                DestinationDirectory = "downloads",
                Category = "default",
                Priority = priority
            }
        };
    }

    private static (Mock<IHubContext<DownloadHub, IDownloadsClient>> Context, Mock<IDownloadsClient> Client)
        CreateDownloadsHub()
    {
        Mock<IDownloadsClient> hubClient = new();
        hubClient.Setup(c => c.DownloadUpdatedAsync(It.IsAny<ContractDownloads.DownloadSummary>()))
            .Returns(Task.CompletedTask);
        hubClient.Setup(c => c.DownloadRemovedAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        hubClient.Setup(c => c.DownloadsClearedAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .Returns(Task.CompletedTask);

        Mock<IHubClients<IDownloadsClient>> clients = new();
        clients.Setup(c => c.Group(DownloadHub.GroupName)).Returns(hubClient.Object);

        Mock<IHubContext<DownloadHub, IDownloadsClient>> context = new();
        context.SetupGet(c => c.Clients).Returns(clients.Object);

        return (context, hubClient);
    }

    private static (Mock<IHubContext<QueueHub, IQueueClient>> Context, Mock<IQueueClient> Client)
        CreateQueueHub()
    {
        Mock<IQueueClient> hubClient = new();
        hubClient.Setup(c => c.QueueSnapshotAsync(It.IsAny<IReadOnlyList<ContractQueue.QueueItem>>()))
            .Returns(Task.CompletedTask);

        Mock<IHubClients<IQueueClient>> clients = new();
        clients.Setup(c => c.Group(QueueHub.GroupName)).Returns(hubClient.Object);

        Mock<IHubContext<QueueHub, IQueueClient>> context = new();
        context.SetupGet(c => c.Clients).Returns(clients.Object);

        return (context, hubClient);
    }
}

/// <summary>
///     A download engine test double that records calls and can be told to throw, so the endpoint
///     exception-to-status-code mapping can be exercised.
/// </summary>
internal sealed class TestDownloadEngine : IDownloadEngine
{
    public List<IDownloadTask> Tasks { get; } = [];

    public List<(Guid Id, CoreModels.DownloadPriority Priority)> PriorityChanges { get; } = [];

    public List<(string Verb, Guid Id)> CommandCalls { get; } = [];

    public List<(Guid Id, bool RemoveFiles)> CancelCalls { get; } = [];

    public Dictionary<string, Exception> CommandExceptions { get; } = [];

    public IDownloadTask? AddedTask { get; set; }

    public Uri? LastAddedUrl { get; private set; }

    public Exception? AddDownloadException { get; set; }

    public Exception? CancelException { get; set; }

    public bool ChangePriorityResult { get; set; } = true;

    public int PauseAllResult { get; set; }

    public int ClearCompletedCallCount { get; private set; }

    public (int Active, int Queued) QueueStats { get; set; }

    public CoreModels.DownloadStateFilter? LastFilter { get; private set; }

    public Task<IDownloadTask> AddDownloadAsync(
        Uri url,
        CoreModels.DownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        LastAddedUrl = url;

        if (AddDownloadException is not null)
        {
            throw AddDownloadException;
        }

        var task = AddedTask ?? throw new InvalidOperationException("AddedTask was not configured.");
        return Task.FromResult(task);
    }

    public Task StartDownloadAsync(Guid taskId, CancellationToken cancellationToken = default)
        => RunCommand("start", taskId);

    public Task PauseDownloadAsync(Guid taskId, CancellationToken cancellationToken = default)
        => RunCommand("pause", taskId);

    public Task ResumeDownloadAsync(Guid taskId, CancellationToken cancellationToken = default)
        => RunCommand("resume", taskId);

    public Task CancelDownloadAsync(
        Guid taskId,
        bool removePartialFiles = false,
        CancellationToken cancellationToken = default)
    {
        if (CancelException is not null)
        {
            throw CancelException;
        }

        CancelCalls.Add((taskId, removePartialFiles));
        return Task.CompletedTask;
    }

    public IDownloadTask? GetDownload(Guid taskId) => Tasks.Find(t => t.Id == taskId);

    public IEnumerable<IDownloadTask> GetDownloads(CoreModels.DownloadStateFilter filter)
    {
        LastFilter = filter;
        return Tasks.Where(t => (filter & ToFilter(t.State)) != 0).ToList();
    }

    public bool ChangePriority(Guid taskId, CoreModels.DownloadPriority newPriority)
    {
        PriorityChanges.Add((taskId, newPriority));
        return ChangePriorityResult;
    }

    public bool MoveUp(Guid taskId) => true;

    public bool MoveDown(Guid taskId) => true;

    public Task<int> PauseAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(PauseAllResult);

    public void ClearCompleted() => ClearCompletedCallCount++;

    public (int Active, int Queued) GetQueueStatistics() => QueueStats;

    public IAsyncEnumerable<CoreModels.DownloadProgress> StreamProgressAsync(
        Guid? taskId = null,
        CancellationToken cancellationToken = default)
        => AsyncEnumerable.Empty<CoreModels.DownloadProgress>();

    private Task RunCommand(string verb, Guid taskId)
    {
        if (CommandExceptions.TryGetValue(verb, out var exception))
        {
            throw exception;
        }

        CommandCalls.Add((verb, taskId));
        return Task.CompletedTask;
    }

    private static CoreModels.DownloadStateFilter ToFilter(CoreModels.DownloadState state) => state switch
    {
        CoreModels.DownloadState.Created => CoreModels.DownloadStateFilter.Created,
        CoreModels.DownloadState.Queued => CoreModels.DownloadStateFilter.Queued,
        CoreModels.DownloadState.Analyzing => CoreModels.DownloadStateFilter.Analyzing,
        CoreModels.DownloadState.Downloading => CoreModels.DownloadStateFilter.Downloading,
        CoreModels.DownloadState.Paused => CoreModels.DownloadStateFilter.Paused,
        CoreModels.DownloadState.Completed => CoreModels.DownloadStateFilter.Completed,
        CoreModels.DownloadState.Failed => CoreModels.DownloadStateFilter.Failed,
        CoreModels.DownloadState.Cancelled => CoreModels.DownloadStateFilter.Cancelled,
        _ => CoreModels.DownloadStateFilter.None
    };
}
