using KuriousLabs.Kurio.Contracts.Queue;

namespace KuriousLabs.Kurio.Contracts.Hubs;

public interface IQueueHub
{
    Task SubscribeQueueAsync(CancellationToken cancellationToken = default);

    Task UnsubscribeQueueAsync(CancellationToken cancellationToken = default);

    Task RequestQueueSnapshotAsync(CancellationToken cancellationToken = default);
}

public interface IQueueClient
{
    Task QueueSnapshotAsync(IReadOnlyList<QueueItem> items);

    Task QueueItemEnqueuedAsync(QueueItem item);

    Task QueueItemRemovedAsync(Guid downloadId);

    Task QueuePositionChangedAsync(QueuePositionChanged change);
}
