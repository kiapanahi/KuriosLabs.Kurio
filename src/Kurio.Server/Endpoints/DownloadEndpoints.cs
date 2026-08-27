using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;
using KuriousLabs.Kurio.Server.Hubs;
using KuriousLabs.Kurio.Server.Mappers;
using KuriousLabs.Kurio.Server.Models;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;

namespace KuriousLabs.Kurio.Server.Endpoints;

/// <summary>
///     Minimal-API endpoints for managing downloads. This is a behaviour-preserving conversion of
///     the former <c>DownloadsController</c>: routes, status codes, problem payloads and SignalR
///     broadcasts are unchanged.
/// </summary>
internal static class DownloadEndpoints
{
    /// <summary>
    ///     Log category used for the endpoint handlers. A static class cannot be used as the
    ///     <see cref="ILogger{TCategoryName}" /> type argument, so the category is spelled out.
    /// </summary>
    private const string LogCategory = "KuriousLabs.Kurio.Server.Endpoints.DownloadEndpoints";

    /// <summary>
    ///     Endpoint name for the "get a single download" route. Used by
    ///     <see cref="TypedResults" />.<c>CreatedAtRoute</c> to build the <c>Location</c> header,
    ///     replacing the MVC <c>CreatedAtAction(nameof(GetDownload), ...)</c> call.
    /// </summary>
    private const string GetDownloadRouteName = "GetDownload";

    /// <summary>
    ///     Maps every <c>/api/downloads</c> route onto the supplied endpoint route builder.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <returns>The same <paramref name="endpoints" /> instance, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapDownloadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/api/downloads")
            .WithTags("Downloads");

        group.MapPost("/", AddDownloadAsync)
            .Produces<DownloadResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", GetDownloads)
            .Produces<List<DownloadResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetDownload)
            .WithName(GetDownloadRouteName)
            .Produces<DownloadResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/start", StartDownloadAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/pause", PauseDownloadAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/resume", ResumeDownloadAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/{id:guid}", CancelDownloadAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/priority", ChangePriorityAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/pause-all", PauseAllAsync)
            .Produces<int>(StatusCodes.Status200OK);

        group.MapPost("/clear-completed", ClearCompletedAsync)
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/statistics", GetStatistics)
            .Produces<QueueStatistics>(StatusCodes.Status200OK);

        return endpoints;
    }

    /// <summary>
    ///     Adds a new download to the queue.
    /// </summary>
    private static async Task<IResult> AddDownloadAsync(
        AddDownloadRequest request,
        IDownloadEngine engine,
        IDownloadQueueManager queueManager,
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        IHubContext<QueueHub, IQueueClient> queueHub,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
            {
                return ApiResults.Problem(
                    httpContext,
                    $"The URL '{request.Url}' is not valid",
                    StatusCodes.Status400BadRequest,
                    "Invalid URL");
            }

            var task = await engine.AddDownloadAsync(
                uri,
                request.ToDownloadOptions(),
                cancellationToken).ConfigureAwait(false);

            // Set priority if different from default
            if (request.Priority != DownloadPriority.Normal)
            {
                engine.ChangePriority(task.Id, request.Priority);
            }

            var response = DownloadResponse.FromTask(task);
            await BroadcastDownloadUpdateAsync(downloadsHub, task).ConfigureAwait(false);
            await BroadcastQueueSnapshotAsync(queueHub, queueManager).ConfigureAwait(false);
            return TypedResults.CreatedAtRoute(response, GetDownloadRouteName, new { id = task.Id });
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger(LogCategory).LogAddDownloadError(ex, request.Url);
            return ApiResults.Problem(
                httpContext,
                ex.Message,
                StatusCodes.Status400BadRequest,
                "Failed to add download");
        }
    }

    /// <summary>
    ///     Gets all downloads with optional filtering.
    /// </summary>
    /// <remarks>
    ///     <paramref name="filter" /> is nullable with an explicit default so that a request
    ///     without a <c>filter</c> query parameter still binds (MVC bound the missing value to
    ///     <see cref="DownloadStateFilter.None" />; minimal APIs would otherwise return 400).
    ///     <see cref="DownloadStateFilterQuery" /> restores MVC's case-insensitive parsing.
    /// </remarks>
    private static Ok<List<DownloadResponse>> GetDownloads(
        IDownloadEngine engine,
        DownloadStateFilterQuery? filter = null)
    {
        var downloads = engine.GetDownloads(filter?.Value ?? DownloadStateFilter.None)
            .Select(DownloadResponse.FromTask)
            .ToList();

        return TypedResults.Ok(downloads);
    }

    /// <summary>
    ///     Gets a specific download by ID.
    /// </summary>
    private static IResult GetDownload(Guid id, IDownloadEngine engine, HttpContext httpContext)
    {
        var task = engine.GetDownload(id);
        if (task == null)
        {
            return ApiResults.Problem(
                httpContext,
                $"No download with ID {id} exists",
                StatusCodes.Status404NotFound,
                "Download not found");
        }

        return TypedResults.Ok(DownloadResponse.FromTask(task));
    }

    /// <summary>
    ///     Starts a queued download.
    /// </summary>
    private static async Task<IResult> StartDownloadAsync(
        Guid id,
        IDownloadEngine engine,
        IDownloadQueueManager queueManager,
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        IHubContext<QueueHub, IQueueClient> queueHub,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await engine.StartDownloadAsync(id, cancellationToken).ConfigureAwait(false);
            await BroadcastDownloadUpdateIfExistsAsync(downloadsHub, engine, id).ConfigureAwait(false);
            await BroadcastQueueSnapshotAsync(queueHub, queueManager).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            loggerFactory.CreateLogger(LogCategory).LogCannotStartDownload(ex, id);
            return ApiResults.Problem(
                httpContext,
                ex.Message,
                StatusCodes.Status400BadRequest,
                "Cannot start download");
        }
        catch (KeyNotFoundException ex)
        {
            loggerFactory.CreateLogger(LogCategory).LogDownloadNotFound(ex, id);
            return ApiResults.Problem(
                httpContext,
                ex.Message,
                StatusCodes.Status404NotFound,
                "Download not found");
        }
    }

    /// <summary>
    ///     Pauses an active download.
    /// </summary>
    private static async Task<IResult> PauseDownloadAsync(
        Guid id,
        IDownloadEngine engine,
        IDownloadQueueManager queueManager,
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        IHubContext<QueueHub, IQueueClient> queueHub,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await engine.PauseDownloadAsync(id, cancellationToken).ConfigureAwait(false);
            await BroadcastDownloadUpdateIfExistsAsync(downloadsHub, engine, id).ConfigureAwait(false);
            await BroadcastQueueSnapshotAsync(queueHub, queueManager).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            loggerFactory.CreateLogger(LogCategory).LogCannotPauseDownload(ex, id);
            return ApiResults.Problem(
                httpContext,
                ex.Message,
                StatusCodes.Status400BadRequest,
                "Cannot pause download");
        }
        catch (KeyNotFoundException ex)
        {
            loggerFactory.CreateLogger(LogCategory).LogDownloadNotFound(ex, id);
            return ApiResults.Problem(
                httpContext,
                ex.Message,
                StatusCodes.Status404NotFound,
                "Download not found");
        }
    }

    /// <summary>
    ///     Resumes a paused download.
    /// </summary>
    private static async Task<IResult> ResumeDownloadAsync(
        Guid id,
        IDownloadEngine engine,
        IDownloadQueueManager queueManager,
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        IHubContext<QueueHub, IQueueClient> queueHub,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await engine.ResumeDownloadAsync(id, cancellationToken).ConfigureAwait(false);
            await BroadcastDownloadUpdateIfExistsAsync(downloadsHub, engine, id).ConfigureAwait(false);
            await BroadcastQueueSnapshotAsync(queueHub, queueManager).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            loggerFactory.CreateLogger(LogCategory).LogCannotResumeDownload(ex, id);
            return ApiResults.Problem(
                httpContext,
                ex.Message,
                StatusCodes.Status400BadRequest,
                "Cannot resume download");
        }
        catch (KeyNotFoundException ex)
        {
            loggerFactory.CreateLogger(LogCategory).LogDownloadNotFound(ex, id);
            return ApiResults.Problem(
                httpContext,
                ex.Message,
                StatusCodes.Status404NotFound,
                "Download not found");
        }
    }

    /// <summary>
    ///     Cancels a download and optionally removes partial files.
    /// </summary>
    private static async Task<IResult> CancelDownloadAsync(
        Guid id,
        IDownloadEngine engine,
        IDownloadQueueManager queueManager,
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        IHubContext<QueueHub, IQueueClient> queueHub,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        bool removeFiles = false)
    {
        try
        {
            await engine.CancelDownloadAsync(id, removeFiles, cancellationToken).ConfigureAwait(false);
            await BroadcastDownloadRemovedAsync(downloadsHub, id).ConfigureAwait(false);
            await BroadcastQueueSnapshotAsync(queueHub, queueManager).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            loggerFactory.CreateLogger(LogCategory).LogCannotCancelDownload(ex, id);
            return ApiResults.Problem(
                httpContext,
                ex.Message,
                StatusCodes.Status400BadRequest,
                "Cannot cancel download");
        }
        catch (KeyNotFoundException ex)
        {
            loggerFactory.CreateLogger(LogCategory).LogDownloadNotFound(ex, id);
            return ApiResults.Problem(
                httpContext,
                ex.Message,
                StatusCodes.Status404NotFound,
                "Download not found");
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger(LogCategory).LogCancelDownloadError(ex, id);
            // Never echo unexpected exception details to the client
            return ApiResults.Problem(
                httpContext,
                "An unexpected error occurred while cancelling the download.",
                StatusCodes.Status500InternalServerError,
                "Failed to cancel download");
        }
    }

    /// <summary>
    ///     Changes the priority of a queued download.
    /// </summary>
    private static async Task<IResult> ChangePriorityAsync(
        Guid id,
        ChangePriorityRequest request,
        IDownloadEngine engine,
        IDownloadQueueManager queueManager,
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        IHubContext<QueueHub, IQueueClient> queueHub,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var success = engine.ChangePriority(id, request.Priority);
        if (!success)
        {
            return ApiResults.Problem(
                httpContext,
                $"Cannot change priority for download {id}",
                StatusCodes.Status404NotFound,
                "Download not found or not queued");
        }

        await BroadcastDownloadUpdateIfExistsAsync(downloadsHub, engine, id).ConfigureAwait(false);
        await BroadcastQueueSnapshotAsync(queueHub, queueManager).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    /// <summary>
    ///     Pauses all active downloads and returns the number of downloads paused.
    /// </summary>
    private static async Task<IResult> PauseAllAsync(
        IDownloadEngine engine,
        IDownloadQueueManager queueManager,
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        IHubContext<QueueHub, IQueueClient> queueHub,
        CancellationToken cancellationToken)
    {
        var count = await engine.PauseAllAsync(cancellationToken).ConfigureAwait(false);

        var active = engine.GetDownloads(DownloadStateFilter.Active).ToList();
        foreach (var task in active)
        {
            await BroadcastDownloadUpdateAsync(downloadsHub, task).ConfigureAwait(false);
        }

        await BroadcastQueueSnapshotAsync(queueHub, queueManager).ConfigureAwait(false);
        return TypedResults.Ok(count);
    }

    /// <summary>
    ///     Clears all completed downloads from the queue.
    /// </summary>
    private static async Task<IResult> ClearCompletedAsync(
        IDownloadEngine engine,
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        CancellationToken cancellationToken)
    {
        var completedIds = engine.GetDownloads(DownloadStateFilter.Completed)
            .Select(d => d.Id)
            .ToList();

        engine.ClearCompleted();

        if (completedIds.Count > 0)
        {
            await BroadcastDownloadsClearedAsync(downloadsHub, completedIds).ConfigureAwait(false);
        }

        return TypedResults.NoContent();
    }

    /// <summary>
    ///     Gets download queue statistics.
    /// </summary>
    private static Ok<QueueStatistics> GetStatistics(IDownloadEngine engine)
    {
        var (active, queued) = engine.GetQueueStatistics();
        var allDownloads = engine.GetDownloads(DownloadStateFilter.All).ToList();

        return TypedResults.Ok(new QueueStatistics
        {
            ActiveDownloads = active,
            QueuedDownloads = queued,
            TotalDownloads = allDownloads.Count,
            CompletedDownloads = allDownloads.Count(d => d.State == DownloadState.Completed),
            FailedDownloads = allDownloads.Count(d => d.State == DownloadState.Failed)
        });
    }

    private static async Task BroadcastDownloadUpdateAsync(
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        IDownloadTask task)
    {
        var summary = task.ToContract();
        await downloadsHub.Clients.Group(DownloadHub.GroupName)
            .DownloadUpdatedAsync(summary)
            .ConfigureAwait(false);
    }

    private static async Task BroadcastDownloadUpdateIfExistsAsync(
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        IDownloadEngine engine,
        Guid id)
    {
        var task = engine.GetDownload(id);
        if (task != null)
        {
            await BroadcastDownloadUpdateAsync(downloadsHub, task).ConfigureAwait(false);
        }
    }

    private static async Task BroadcastDownloadRemovedAsync(
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        Guid id)
    {
        await downloadsHub.Clients.Group(DownloadHub.GroupName)
            .DownloadRemovedAsync(id)
            .ConfigureAwait(false);
    }

    private static async Task BroadcastDownloadsClearedAsync(
        IHubContext<DownloadHub, IDownloadsClient> downloadsHub,
        IReadOnlyCollection<Guid> ids)
    {
        await downloadsHub.Clients.Group(DownloadHub.GroupName)
            .DownloadsClearedAsync(ids)
            .ConfigureAwait(false);
    }

    private static async Task BroadcastQueueSnapshotAsync(
        IHubContext<QueueHub, IQueueClient> queueHub,
        IDownloadQueueManager queueManager)
    {
        var snapshot = queueManager.GetQueuedTasks()
            .Select((task, index) => task.ToContract(index + 1))
            .ToList();

        await queueHub.Clients.Group(QueueHub.GroupName)
            .QueueSnapshotAsync(snapshot)
            .ConfigureAwait(false);
    }
}
