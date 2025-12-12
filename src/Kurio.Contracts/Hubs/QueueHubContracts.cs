using KuriousLabs.Kurio.Contracts.Queue;

namespace KuriousLabs.Kurio.Contracts.Hubs;

public interface IQueueHub
{
    Task SubscribeQueueAsync();

    Task UnsubscribeQueueAsync();

    Task RequestQueueSnapshotAsync();
}

public interface IQueueClient
{
    Task QueueSnapshotAsync(IReadOnlyList<QueueItem> items);

    Task QueueItemEnqueuedAsync(QueueItem item);

    Task QueueItemRemovedAsync(Guid downloadId);

    Task QueuePositionChangedAsync(QueuePositionChanged change);
}
