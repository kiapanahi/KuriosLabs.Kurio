using KuriousLabs.Kurio.Contracts.Downloads;

namespace KuriousLabs.Kurio.Contracts.Queue;

public sealed record QueueItem
{
    public Guid DownloadId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Category { get; init; }

    public int Position { get; init; }

    public DownloadPriority Priority { get; init; } = DownloadPriority.Normal;

    public DateTimeOffset AddedAt { get; init; }

    public DateTimeOffset? ScheduledAt { get; init; }
}

public sealed record QueueSnapshot
{
    public IReadOnlyList<QueueItem> Items { get; init; } = Array.Empty<QueueItem>();
}

public sealed record QueuePositionChanged
{
    public Guid DownloadId { get; init; }

    public int Position { get; init; }

    public DownloadPriority Priority { get; init; } = DownloadPriority.Normal;
}
