using System.Linq;

using KuriousLabs.Kurio.Contracts.Downloads;
using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Mappers;
using CoreModels = KuriousLabs.Kurio.Core.Models;

using Microsoft.AspNetCore.SignalR;

namespace KuriousLabs.Kurio.Server.Hubs;

public sealed class DownloadHub : Hub<IDownloadsClient>, IDownloadsHub
{
    public const string GroupName = "downloads";

    private readonly IDownloadEngine _engine;
    private readonly ILogger<DownloadHub> _logger;

    public DownloadHub(
        IDownloadEngine engine,
        ILogger<DownloadHub> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public async Task SubscribeDownloadsAsync(DownloadSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Client {ConnectionId} subscribed to downloads", Context.ConnectionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName, cancellationToken).ConfigureAwait(false);
        await SendSnapshotAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task UnsubscribeDownloadsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Client {ConnectionId} unsubscribed from downloads", Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName, cancellationToken).ConfigureAwait(false);
    }

    public async Task RequestSnapshotAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Client {ConnectionId} requested snapshot", Context.ConnectionId);
        await SendSnapshotAsync(null, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendSnapshotAsync(DownloadSubscriptionRequest? request, CancellationToken cancellationToken)
    {
        var filter = request?.States ?? DownloadStateFilter.All;
        var tasks = _engine.GetDownloads(filter.ToCoreFilter())
            .Where(task => request?.Category is null || task.Options.Category == request.Category)
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.CreatedAt)
            .ToList();

        if (request?.IncludeCompleted == false)
        {
            tasks = tasks
                .Where(t => t.State != CoreModels.DownloadState.Completed)
                .ToList();
        }

        if (request?.Limit is { } limit && limit > 0)
        {
            tasks = tasks.Take(limit).ToList();
        }

        var snapshot = tasks
            .Select(t => t.ToContract())
            .ToList();

        await Clients.Caller.ReceiveSnapshotAsync(snapshot).ConfigureAwait(false);
    }
}
