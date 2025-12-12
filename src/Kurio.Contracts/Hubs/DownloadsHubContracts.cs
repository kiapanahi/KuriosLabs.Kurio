using KuriousLabs.Kurio.Contracts.Downloads;

namespace KuriousLabs.Kurio.Contracts.Hubs;

public interface IDownloadsHub
{
    Task SubscribeDownloadsAsync(DownloadSubscriptionRequest request);

    Task UnsubscribeDownloadsAsync();

    Task RequestSnapshotAsync();
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
