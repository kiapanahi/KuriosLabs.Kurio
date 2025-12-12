using KuriousLabs.Kurio.Contracts.Queue;
using KuriousLabs.Kurio.Core.Abstractions;

namespace KuriousLabs.Kurio.Server.Mappers;

public static class QueueContractMapper
{
    public static QueueItem ToContract(this IDownloadTask task, int position)
    {
        ArgumentNullException.ThrowIfNull(task);

        return new QueueItem
        {
            DownloadId = task.Id,
            Name = task.FileName,
            Category = task.Options.Category,
            Position = position,
            Priority = DownloadContractMapper.MapPriority(task.Priority),
            AddedAt = task.CreatedAt,
            ScheduledAt = null
        };
    }
}
