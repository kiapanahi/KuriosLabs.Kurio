using KuriousLabs.Kurio.Contracts.Stats;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Server.Mappers;

public static class StatsContractMapper
{
    public static StatsSnapshot ToContract(
        this DownloadStatistics statistics,
        IDownloadQueueManager queueManager)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(queueManager);

        return new StatsSnapshot
        {
            ActiveCount = queueManager.ActiveDownloadsCount,
            QueuedCount = queueManager.QueuedDownloadsCount,
            CompletedCount = statistics.AllTimeCompletedDownloads,
            FailedCount = statistics.AllTimeFailedDownloads,
            CurrentThroughputBytesPerSecond = statistics.AverageDownloadSpeed,
            AverageThroughputBytesPerSecond = statistics.AverageDownloadSpeed,
            TotalBytesDownloaded = statistics.AllTimeBytesDownloaded,
            StartedAt = statistics.SessionStartedAt
        };
    }
}
