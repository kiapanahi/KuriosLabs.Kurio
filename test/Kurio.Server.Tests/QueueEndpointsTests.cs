using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Contracts.Queue;
using KuriousLabs.Kurio.Server.Hubs;

using Microsoft.AspNetCore.SignalR;

using Moq;

using ContractDownloads = KuriousLabs.Kurio.Contracts.Downloads;
using CoreModels = KuriousLabs.Kurio.Core.Models;

namespace Kurio.Server.Tests;

public class QueueEndpointsTests
{
    [Fact]
    public async Task QueueSnapshot_ReturnsOrderedItems()
    {
        using var factory = new ServerTestFactory();
        var client = factory.WithQueue(queue =>
        {
            queue.Seed(
                FakeDownloadTask.Create("first", CoreModels.DownloadPriority.High),
                FakeDownloadTask.Create("second", CoreModels.DownloadPriority.Normal));
        }).CreateClient();

        var response = await client.GetAsync("/api/queue");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = await response.Content.ReadFromJsonAsync<List<QueueItem>>();
        snapshot.Should().NotBeNull();
        snapshot!.Count.Should().Be(2);
        snapshot[0].Position.Should().Be(1);
        snapshot[0].Priority.Should().Be(ContractDownloads.DownloadPriority.High);
        snapshot[1].Position.Should().Be(2);
        snapshot[1].Priority.Should().Be(ContractDownloads.DownloadPriority.Normal);
    }

    [Fact]
    public async Task ChangePriority_ReordersQueue()
    {
        var low = FakeDownloadTask.Create("low", CoreModels.DownloadPriority.Low);
        var normal = FakeDownloadTask.Create("normal", CoreModels.DownloadPriority.Normal);

        using var factory = new ServerTestFactory();
        var client = factory.WithQueue(queue => queue.Seed(low, normal)).CreateClient();

        var changeRequest = new ContractDownloads.ChangePriorityRequest { Priority = ContractDownloads.DownloadPriority.High };
        var response = await client.PostAsJsonAsync($"/api/queue/{low.Id}/priority", changeRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var snapshot = await client.GetFromJsonAsync<List<QueueItem>>("/api/queue");
        snapshot.Should().NotBeNull();
        snapshot!.Count.Should().Be(2);
        snapshot[0].DownloadId.Should().Be(low.Id);
        snapshot[0].Priority.Should().Be(ContractDownloads.DownloadPriority.High);
        snapshot[0].Position.Should().Be(1);
    }

    [Fact]
    public async Task QueueSnapshot_ReturnsEmptyJsonArray_WhenNothingQueued()
    {
        using var factory = new ServerTestFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/queue");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    [Fact]
    public async Task QueueSnapshot_UsesCamelCaseNamesAndStringEnums()
    {
        using var factory = new ServerTestFactory();
        var client = factory
            .WithQueue(queue => queue.Seed(FakeDownloadTask.Create("only", CoreModels.DownloadPriority.High)))
            .CreateClient();

        var payload = await client.GetStringAsync("/api/queue");

        using var document = JsonDocument.Parse(payload);
        var item = document.RootElement.EnumerateArray().Single();

        item.EnumerateObject().Select(p => p.Name)
            .Should().Contain(["downloadId", "name", "category", "position", "priority", "addedAt"]);
        item.GetProperty("priority").GetString().Should().Be("High");
        item.GetProperty("position").GetInt32().Should().Be(1);
        item.GetProperty("name").GetString().Should().Be("only");
    }

    [Fact]
    public async Task ChangePriority_ReturnsProblemDetails_WhenTaskIsNotQueued()
    {
        var unknownId = Guid.NewGuid();

        using var factory = new ServerTestFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/queue/{unknownId}/priority",
            new ContractDownloads.ChangePriorityRequest { Priority = ContractDownloads.DownloadPriority.High });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("title").GetString().Should().Be("Download not found or not queued");
        root.GetProperty("status").GetInt32().Should().Be(404);
        root.GetProperty("detail").GetString().Should().Be($"Cannot change priority for download {unknownId}");
        root.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("move-up", false)]
    [InlineData("move-down", true)]
    [InlineData("move-top", false)]
    [InlineData("move-bottom", true)]
    public async Task MoveEndpoints_ReturnNoContent_WhenQueueWasReordered(string route, bool moveTheHeadItem)
    {
        var high = FakeDownloadTask.Create("high", CoreModels.DownloadPriority.High);
        var normal = FakeDownloadTask.Create("normal", CoreModels.DownloadPriority.Normal);

        using var factory = new ServerTestFactory();
        var client = factory.WithQueue(queue => queue.Seed(high, normal)).CreateClient();

        var targetId = moveTheHeadItem ? high.Id : normal.Id;
        var response = await client.PostAsync($"/api/queue/{targetId}/{route}", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var snapshot = await client.GetFromJsonAsync<List<QueueItem>>("/api/queue");
        snapshot.Should().NotBeNull();
        snapshot!.Select(i => i.Position).Should().Equal(1, 2);
    }

    [Theory]
    [InlineData("move-up", "Download not found or already at top", "up")]
    [InlineData("move-down", "Download not found or already at bottom", "down")]
    [InlineData("move-top", "Download not found or already at top", "to top")]
    [InlineData("move-bottom", "Download not found or already at bottom", "to bottom")]
    public async Task MoveEndpoints_ReturnProblemDetails_WhenQueueManagerRefuses(
        string route,
        string expectedTitle,
        string expectedDirection)
    {
        var unknownId = Guid.NewGuid();

        using var factory = new ServerTestFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/queue/{unknownId}/{route}", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("title").GetString().Should().Be(expectedTitle);
        root.GetProperty("status").GetInt32().Should().Be(404);
        root.GetProperty("detail").GetString().Should().Be($"Cannot move download {unknownId} {expectedDirection}");
        root.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MoveEndpoints_BroadcastSnapshotToQueueHubGroup_BeforeReturning()
    {
        var high = FakeDownloadTask.Create("high", CoreModels.DownloadPriority.High);
        var normal = FakeDownloadTask.Create("normal", CoreModels.DownloadPriority.Normal);

        var queueClient = new Mock<IQueueClient>();
        IReadOnlyList<QueueItem>? broadcast = null;
        queueClient
            .Setup(c => c.QueueSnapshotAsync(It.IsAny<IReadOnlyList<QueueItem>>()))
            .Callback<IReadOnlyList<QueueItem>>(items => broadcast = items)
            .Returns(Task.CompletedTask);

        var hubClients = new Mock<IHubClients<IQueueClient>>();
        hubClients.Setup(c => c.Group(QueueHub.GroupName)).Returns(queueClient.Object);

        var hubContext = new Mock<IHubContext<QueueHub, IQueueClient>>();
        hubContext.SetupGet(h => h.Clients).Returns(hubClients.Object);

        using var factory = new ServerTestFactory();
        var client = factory
            .WithQueue(queue => queue.Seed(high, normal))
            .WithService(hubContext.Object)
            .CreateClient();

        var response = await client.PostAsync($"/api/queue/{normal.Id}/move-top", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The snapshot must already have been pushed by the time the response is observed.
        queueClient.Verify(c => c.QueueSnapshotAsync(It.IsAny<IReadOnlyList<QueueItem>>()), Times.Once);
        broadcast.Should().NotBeNull();
        broadcast!.Select(i => i.DownloadId).Should().Equal(normal.Id, high.Id);
        broadcast.Select(i => i.Position).Should().Equal(1, 2);
    }

    [Fact]
    public async Task FailedMove_DoesNotBroadcastSnapshot()
    {
        var queueClient = new Mock<IQueueClient>();
        var hubClients = new Mock<IHubClients<IQueueClient>>();
        hubClients.Setup(c => c.Group(QueueHub.GroupName)).Returns(queueClient.Object);

        var hubContext = new Mock<IHubContext<QueueHub, IQueueClient>>();
        hubContext.SetupGet(h => h.Clients).Returns(hubClients.Object);

        using var factory = new ServerTestFactory();
        var client = factory.WithService(hubContext.Object).CreateClient();

        var response = await client.PostAsync($"/api/queue/{Guid.NewGuid()}/move-up", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        queueClient.Verify(c => c.QueueSnapshotAsync(It.IsAny<IReadOnlyList<QueueItem>>()), Times.Never);
    }

    [Fact]
    public async Task UnknownQueueRoute_ReturnsNotFound()
    {
        using var factory = new ServerTestFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/queue/not-a-guid/move-up", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
