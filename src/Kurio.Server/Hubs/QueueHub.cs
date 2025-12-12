using System.Linq;

using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Contracts.Queue;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Mappers;

using Microsoft.AspNetCore.SignalR;

namespace KuriousLabs.Kurio.Server.Hubs;

public sealed class QueueHub : Hub<IQueueClient>, IQueueHub
{
    public const string GroupName = "queue";

    private readonly IDownloadQueueManager _queueManager;
    private readonly ILogger<QueueHub> _logger;

    public QueueHub(
        IDownloadQueueManager queueManager,
        ILogger<QueueHub> logger)
    {
        _queueManager = queueManager;
        _logger = logger;
    }

    public async Task SubscribeQueueAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Client {ConnectionId} subscribed to queue", Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName, cancellationToken).ConfigureAwait(false);
        await SendSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UnsubscribeQueueAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Client {ConnectionId} unsubscribed from queue", Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName, cancellationToken).ConfigureAwait(false);
    }

    public async Task RequestQueueSnapshotAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Client {ConnectionId} requested queue snapshot", Context.ConnectionId);
        await SendSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SendSnapshotAsync(CancellationToken cancellationToken)
    {
        var queueItems = _queueManager.GetQueuedTasks()
            .Select((task, index) => task.ToContract(index + 1))
            .ToList();

        await Clients.Caller.QueueSnapshotAsync(queueItems).ConfigureAwait(false);
    }
}
