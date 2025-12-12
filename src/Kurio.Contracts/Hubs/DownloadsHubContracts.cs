using KuriousLabs.Kurio.Contracts.Downloads;

namespace KuriousLabs.Kurio.Contracts.Hubs;

public interface IDownloadsHub
{
    Task SubscribeDownloadsAsync(DownloadSubscriptionRequest request, CancellationToken cancellationToken = default);

    Task UnsubscribeDownloadsAsync(CancellationToken cancellationToken = default);

    Task RequestSnapshotAsync(CancellationToken cancellationToken = default);
}

public interface IDownloadsClient
{
    Task ReceiveSnapshotAsync(IReadOnlyList<DownloadSummary> downloads);

    Task DownloadUpdatedAsync(DownloadSummary download);

    Task DownloadProgressedAsync(DownloadProgressUpdate progress);

    Task DownloadStatusChangedAsync(DownloadStatusChange change);

    Task DownloadRemovedAsync(Guid downloadId);

    Task DownloadsClearedAsync(IReadOnlyCollection<Guid> downloadIds);
}
