using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Contracts.Queue;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Hubs;
using KuriousLabs.Kurio.Server.Mappers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using ContractChangePriorityRequest = KuriousLabs.Kurio.Contracts.Downloads.ChangePriorityRequest;

namespace KuriousLabs.Kurio.Server.Endpoints;

/// <summary>
///     Minimal-API endpoints for inspecting and reordering the download queue, replacing the
///     former <c>QueueController</c>. The routes, status codes and error payloads are unchanged.
/// </summary>
internal static class QueueEndpoints
{
    /// <summary>
    ///     Maps the <c>/api/queue</c> endpoint group: the queue snapshot plus the priority and
    ///     move operations.
    /// </summary>
    /// <param name="endpoints">The route builder to register the endpoints on.</param>
    /// <returns>The same <paramref name="endpoints" /> instance, for chaining.</returns>
    public static IEndpointRouteBuilder MapQueueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/queue")
            .WithTags("Queue");

        group.MapGet("/", (IDownloadQueueManager queueManager) => TypedResults.Ok(BuildSnapshot(queueManager)))
            .WithName("GetQueue")
            .Produces<List<QueueItem>>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/priority", async (
                HttpContext httpContext,
                Guid id,
                ContractChangePriorityRequest request,
                IDownloadQueueManager queueManager,
                IHubContext<QueueHub, IQueueClient> queueHubContext) =>
            {
                var success = queueManager.ChangePriority(id, request.Priority.ToCorePriority());
                return await CompleteAsync(
                        httpContext,
                        queueManager,
                        queueHubContext,
                        success,
                        $"Cannot change priority for download {id}",
                        "Download not found or not queued")
                    .ConfigureAwait(false);
            })
            .WithName("ChangeQueuePriority")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/move-up", async (
                HttpContext httpContext,
                Guid id,
                IDownloadQueueManager queueManager,
                IHubContext<QueueHub, IQueueClient> queueHubContext) =>
                await CompleteAsync(
                        httpContext,
                        queueManager,
                        queueHubContext,
                        queueManager.MoveUp(id),
                        $"Cannot move download {id} up",
                        "Download not found or already at top")
                    .ConfigureAwait(false))
            .WithName("MoveQueueItemUp")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/move-down", async (
                HttpContext httpContext,
                Guid id,
                IDownloadQueueManager queueManager,
                IHubContext<QueueHub, IQueueClient> queueHubContext) =>
                await CompleteAsync(
                        httpContext,
                        queueManager,
                        queueHubContext,
                        queueManager.MoveDown(id),
                        $"Cannot move download {id} down",
                        "Download not found or already at bottom")
                    .ConfigureAwait(false))
            .WithName("MoveQueueItemDown")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/move-top", async (
                HttpContext httpContext,
                Guid id,
                IDownloadQueueManager queueManager,
                IHubContext<QueueHub, IQueueClient> queueHubContext) =>
                await CompleteAsync(
                        httpContext,
                        queueManager,
                        queueHubContext,
                        queueManager.MoveToTop(id),
                        $"Cannot move download {id} to top",
                        "Download not found or already at top")
                    .ConfigureAwait(false))
            .WithName("MoveQueueItemToTop")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/move-bottom", async (
                HttpContext httpContext,
                Guid id,
                IDownloadQueueManager queueManager,
                IHubContext<QueueHub, IQueueClient> queueHubContext) =>
                await CompleteAsync(
                        httpContext,
                        queueManager,
                        queueHubContext,
                        queueManager.MoveToBottom(id),
                        $"Cannot move download {id} to bottom",
                        "Download not found or already at bottom")
                    .ConfigureAwait(false))
            .WithName("MoveQueueItemToBottom")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>
    ///     Shared tail of every mutating queue operation: a failed reorder becomes a 404
    ///     <see cref="ProblemDetails" />, a successful one broadcasts the refreshed snapshot to
    ///     subscribed hub clients before returning <c>204 No Content</c>.
    /// </summary>
    private static async Task<IResult> CompleteAsync(
        HttpContext httpContext,
        IDownloadQueueManager queueManager,
        IHubContext<QueueHub, IQueueClient> queueHubContext,
        bool success,
        string detail,
        string title)
    {
        if (!success)
        {
            return ApiResults.Problem(httpContext, detail, StatusCodes.Status404NotFound, title);
        }

        await BroadcastSnapshotAsync(queueManager, queueHubContext).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    /// <summary>Projects the queued tasks onto contract DTOs with 1-based queue positions.</summary>
    private static List<QueueItem> BuildSnapshot(IDownloadQueueManager queueManager)
    {
        return queueManager.GetQueuedTasks()
            .Select((task, index) => task.ToContract(index + 1))
            .ToList();
    }

    /// <summary>Pushes the current queue snapshot to the queue hub group.</summary>
    private static async Task BroadcastSnapshotAsync(
        IDownloadQueueManager queueManager,
        IHubContext<QueueHub, IQueueClient> queueHubContext)
    {
        var snapshot = BuildSnapshot(queueManager);
        await queueHubContext.Clients.Group(QueueHub.GroupName)
            .QueueSnapshotAsync(snapshot)
            .ConfigureAwait(false);
    }
}
